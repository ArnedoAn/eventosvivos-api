using EventosVivos.Domain.Abstractions;
using EventosVivos.Infrastructure.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventosVivos.Infrastructure.Tests.Options;

public class ReservationOptionsTests
{
    [Fact]
    public void Defaults_match_expected_values()
    {
        IReservationOptions options = new ReservationOptions();

        options.PendingHoldsInventory.Should().BeTrue();
        options.PendingExpirationMinutes.Should().Be(0);
    }

    [Fact]
    public void Binds_from_Reservation_configuration_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservation:PendingHoldsInventory"] = "false",
                ["Reservation:PendingExpirationMinutes"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ReservationOptions>(config.GetSection("Reservation"));

        var provider = services.BuildServiceProvider();
        var bound = provider.GetRequiredService<IOptions<ReservationOptions>>().Value;

        bound.PendingHoldsInventory.Should().BeFalse();
        bound.PendingExpirationMinutes.Should().Be(15);
    }
}
