using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ConferenceRoomRental.Application.Bookings;
using ConferenceRoomRental.Application.Reports;
using ConferenceRoomRental.Application.Rooms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace ConferenceRoomRental.Api.IntegrationTests;

public sealed class ConferenceRoomApiTests : IClassFixture<ConferenceRoomApiFactory>
{
    private readonly HttpClient _client;

    public ConferenceRoomApiTests(ConferenceRoomApiFactory factory)
    {
        _client = factory.Client;
    }

    [Fact]
    public async Task OperationalEndpoints_WhenApplicationIsReady_ReturnHealthAndDocumentation()
    {
        using HttpResponseMessage healthResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        using HttpResponseMessage openApiResponse = await _client.GetAsync("/openapi/v1.json", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        string openApiDocument = await openApiResponse.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("/api/v1/bookings", openApiDocument, StringComparison.Ordinal);

        using HttpResponseMessage swaggerResponse = await _client.GetAsync("/swagger/index.html", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
    }

    [Fact]
    public async Task BookingWorkflow_CreatesRoomBlocksOverlapAndFeedsReports()
    {
        RoomDto room = await CreateRoomAsync();
        DateOnly bookingDate = FutureBookingDate();
        string availabilityUrl = AvailabilityUrl(bookingDate, minimumCapacity: 40);

        RoomDto[] availableBefore = await GetRequiredAsync<RoomDto[]>(availabilityUrl);
        Assert.Contains(availableBefore, item => item.Id == room.Id);

        object bookingRequest = BookingRequest(room, bookingDate);
        using HttpResponseMessage createBookingResponse = await _client.PostAsJsonAsync(
            "/api/v1/bookings",
            bookingRequest,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createBookingResponse.StatusCode);
        BookingDto booking = await ReadRequiredAsync<BookingDto>(createBookingResponse);
        Assert.Equal(5_100m, booking.TotalPrice);

        BookingDto persistedBooking = await GetRequiredAsync<BookingDto>($"/api/v1/bookings/{booking.Id}");
        Assert.Equal(booking.Id, persistedBooking.Id);
        Assert.Equal(booking.RoomId, persistedBooking.RoomId);
        Assert.Equal(booking.TotalPrice, persistedBooking.TotalPrice);
        Assert.Equal<SelectedServiceDto>(booking.Services, persistedBooking.Services);
        Assert.Equal<PriceSegmentDto>(booking.PriceBreakdown, persistedBooking.PriceBreakdown);

        RoomDto[] availableAfter = await GetRequiredAsync<RoomDto[]>(availabilityUrl);
        Assert.DoesNotContain(availableAfter, item => item.Id == room.Id);

        using HttpResponseMessage overlapResponse = await _client.PostAsJsonAsync(
            "/api/v1/bookings",
            bookingRequest,
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);

        RevenueReportDto revenue = await GetRequiredAsync<RevenueReportDto>(
            ReportUrl("revenue", bookingDate));
        Assert.Contains(revenue.ByRoom, row => row.RoomId == room.Id && row.Revenue == 5_100m);

        UtilizationReportDto utilization = await GetRequiredAsync<UtilizationReportDto>(
            ReportUrl("utilization", bookingDate));
        Assert.Contains(
            utilization.Rooms,
            row => row.RoomId == room.Id && row.BookedHours == 2m && row.UtilizationPercent == 11.76m);
    }

    [Fact]
    public async Task ConcurrentBookingRequests_WhenTimeRangeMatches_CreateExactlyOneBooking()
    {
        RoomDto room = await CreateRoomAsync();
        object bookingRequest = BookingRequest(room, FutureBookingDate());

        Task<HttpResponseMessage> firstRequest = _client.PostAsJsonAsync(
            "/api/v1/bookings",
            bookingRequest,
            CancellationToken.None);
        Task<HttpResponseMessage> secondRequest = _client.PostAsJsonAsync(
            "/api/v1/bookings",
            bookingRequest,
            CancellationToken.None);

        await Task.WhenAll(firstRequest, secondRequest);
        using HttpResponseMessage firstResponse = await firstRequest;
        using HttpResponseMessage secondResponse = await secondRequest;
        HttpStatusCode[] statusCodes = [firstResponse.StatusCode, secondResponse.StatusCode];

        Assert.Single(statusCodes, status => status == HttpStatusCode.Created);
        Assert.Single(statusCodes, status => status == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ArchiveRoom_WhenFutureBookingExists_ReturnsConflictAndKeepsRoomActive()
    {
        RoomDto room = await CreateRoomAsync();
        using HttpResponseMessage bookingResponse = await _client.PostAsJsonAsync(
            "/api/v1/bookings",
            BookingRequest(room, FutureBookingDate()),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);

        using HttpResponseMessage archiveResponse = await _client.DeleteAsync(
            $"/api/v1/rooms/{room.Id}",
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Conflict, archiveResponse.StatusCode);
        Assert.Equal("application/problem+json", archiveResponse.Content.Headers.ContentType?.MediaType);

        RoomDto activeRoom = await GetRequiredAsync<RoomDto>($"/api/v1/rooms/{room.Id}");
        Assert.Equal(room.Id, activeRoom.Id);
    }

    [Fact]
    public async Task CreateBooking_WhenServiceIdentifierRepeats_ReturnsValidationProblemDetails()
    {
        RoomDto room = await CreateRoomAsync();
        Guid serviceId = Assert.Single(room.Services).Id;
        object request = BookingRequest(room, FutureBookingDate(), [serviceId, serviceId]);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/bookings",
            request,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        ValidationProblemDetails problem = await ReadRequiredAsync<ValidationProblemDetails>(response);
        Assert.Contains("selectedServiceIds", problem.Errors.Keys);
    }

    [Fact]
    public async Task RoomLifecycle_WhenNoFutureBooking_UpdatesAndArchivesRoom()
    {
        RoomDto room = await CreateRoomAsync();
        Guid serviceId = Assert.Single(room.Services).Id;

        using HttpResponseMessage updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/rooms/{room.Id}",
            new
            {
                name = $"{room.Name} updated",
                capacity = 75,
                baseHourlyRate = 2_500m,
                services = new[] { new { name = "Projector", price = 650m } },
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        RoomDto updated = await ReadRequiredAsync<RoomDto>(updateResponse);
        Assert.Equal(75, updated.Capacity);
        Assert.Equal(serviceId, Assert.Single(updated.Services).Id);

        using HttpResponseMessage deleteResponse = await _client.DeleteAsync(
            $"/api/v1/rooms/{room.Id}",
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using HttpResponseMessage getResponse = await _client.GetAsync(
            $"/api/v1/rooms/{room.Id}",
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateRoom_WhenServiceCatalogueChanges_ReplacesServices()
    {
        RoomDto room = await CreateRoomAsync();

        using HttpResponseMessage updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/rooms/{room.Id}",
            new
            {
                name = room.Name,
                capacity = room.Capacity,
                baseHourlyRate = room.BaseHourlyRate,
                services = new[] { new { name = "Sound system", price = 750m } },
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ServiceDto service = Assert.Single((await ReadRequiredAsync<RoomDto>(updateResponse)).Services);
        Assert.Equal("Sound system", service.Name);
        Assert.Equal(750m, service.Price);

        RoomDto persisted = await GetRequiredAsync<RoomDto>($"/api/v1/rooms/{room.Id}");
        Assert.Equal(service, Assert.Single(persisted.Services));
    }

    [Fact]
    public async Task ListRooms_WhenPaginationIsOmitted_UsesDocumentedDefaults()
    {
        PagedRoomsDto result = await GetRequiredAsync<PagedRoomsDto>("/api/v1/rooms");

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Theory]
    [InlineData("/api/v1/reports/revenue")]
    [InlineData("/api/v1/reports/utilization?from=2030-01-01")]
    [InlineData("/api/v1/rooms/available?start=10:00&end=11:00&minimumCapacity=1")]
    public async Task QueryEndpoints_WhenRequiredValuesAreMissing_ReturnBadRequest(string path)
    {
        using HttpResponseMessage response = await _client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<RoomDto> CreateRoomAsync()
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/rooms",
            new
            {
                name = $"Integration room {Guid.NewGuid():N}",
                capacity = 50,
                baseHourlyRate = 2_000m,
                services = new[] { new { name = "Projector", price = 500m } },
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadRequiredAsync<RoomDto>(response);
    }

    private async Task<T> GetRequiredAsync<T>(string path)
    {
        T? result = await _client.GetFromJsonAsync<T>(path, CancellationToken.None);
        return result ?? throw new InvalidOperationException($"Response from '{path}' was empty.");
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response)
    {
        T? result = await response.Content.ReadFromJsonAsync<T>(CancellationToken.None);
        return result ?? throw new InvalidOperationException("Response body was empty.");
    }

    private static object BookingRequest(
        RoomDto room,
        DateOnly bookingDate,
        IReadOnlyCollection<Guid>? selectedServiceIds = null) =>
        new
        {
            roomId = room.Id,
            date = bookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            start = "12:00:00",
            durationMinutes = 120,
            attendeeCount = 40,
            selectedServiceIds = selectedServiceIds ?? [Assert.Single(room.Services).Id],
        };

    private static string AvailabilityUrl(DateOnly date, int minimumCapacity) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"/api/v1/rooms/available?date={date:yyyy-MM-dd}&start=12:00&end=14:00&minimumCapacity={minimumCapacity}");

    private static string ReportUrl(string report, DateOnly date) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"/api/v1/reports/{report}?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");

    private static DateOnly FutureBookingDate() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
}

public sealed class ConferenceRoomApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("conference_room_rental_tests")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private HttpClient? _client;

    public HttpClient Client => _client ?? throw new InvalidOperationException("The test fixture has not started yet.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync(CancellationToken.None);
        _client = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _postgres.GetConnectionString(),
                ["Database:ApplyMigrations"] = "true",
                ["Database:Seed"] = "false",
                ["RateLimiting:PermitLimit"] = "1000",
            });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _client?.Dispose();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
