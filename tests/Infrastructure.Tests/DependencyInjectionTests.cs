using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Infrastructure.Options;
using EventosVivos.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventosVivos.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_registers_expected_services()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=eventosvivos_test;Username=postgres;Password=postgres"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAppDbContext>().Should().NotBeNull();
        provider.GetRequiredService<IClock>().Should().BeOfType<SystemClock>();
        provider.GetRequiredService<IReservationOptions>().Should().NotBeNull();
        provider.GetRequiredService<IJwtTokenService>().Should().BeOfType<JwtTokenService>();
        provider.GetRequiredService<IPasswordHasher>().Should().BeOfType<PasswordHasher>();
        provider.GetRequiredService<IVenueScheduleChecker>().Should().NotBeNull();
        provider.GetRequiredService<IConcurrencyRetryPolicy>().Should().NotBeNull();

        provider.GetRequiredService<IOptions<ReservationOptions>>().Should().NotBeNull();
    }
}
