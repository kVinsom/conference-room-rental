using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Infrastructure.Persistence;
using ConferenceRoomRental.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceRoomRental.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            IConfiguration currentConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            string connectionString = currentConfiguration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null));
        });

        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
