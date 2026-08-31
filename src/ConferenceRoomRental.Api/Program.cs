using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ConferenceRoomRental.Api.Infrastructure;
using ConferenceRoomRental.Application.Bookings;
using ConferenceRoomRental.Application.Reports;
using ConferenceRoomRental.Application.Rooms;
using ConferenceRoomRental.Infrastructure;
using ConferenceRoomRental.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
    });
builder.Services.AddOpenApi();

int permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        IProblemDetailsService problemDetailsService = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new()
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Rate limit exceeded",
                Detail = "Too many requests were received. Retry after the current rate-limit window.",
                Instance = context.HttpContext.Request.Path,
            },
        });
    };
    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

string timeZoneId = builder.Configuration.GetValue<string>("Business:TimeZone") ?? "Europe/Kyiv";
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<CreateRoomHandler>();
builder.Services.AddScoped<UpdateRoomHandler>();
builder.Services.AddScoped<DeleteRoomHandler>();
builder.Services.AddScoped<GetRoomHandler>();
builder.Services.AddScoped<ListRoomsHandler>();
builder.Services.AddScoped<SearchAvailableRoomsHandler>();
builder.Services.AddScoped<CreateBookingHandler>();
builder.Services.AddScoped<GetBookingHandler>();
builder.Services.AddScoped<RevenueReportHandler>();
builder.Services.AddScoped<UtilizationReportHandler>();
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql");

WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Configuration.GetValue("OpenApi:Enabled", true))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Conference Room Rental API v1");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
    });
}

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapControllers().RequireRateLimiting("api");

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    DatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(
        app.Configuration.GetValue("Database:ApplyMigrations", true),
        app.Configuration.GetValue("Database:Seed", true),
        app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
