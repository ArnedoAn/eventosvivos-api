using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Infrastructure.Options;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Seed;
using EventosVivos.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventosVivos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        SeedPasswords.Initialize(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<IAppDbContext, AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<ReservationOptions>(configuration.GetSection("Reservation"));
        services.AddSingleton<IReservationOptions>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReservationOptions>>().Value);

        services.AddSingleton<IClock, SystemClock>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IVenueScheduleChecker, EfVenueScheduleChecker>();
        services.AddScoped<IConcurrencyRetryPolicy, ConcurrencyRetryPolicy>();

        return services;
    }
}
