using EventosVivos.Application;
using EventosVivos.Domain.Rules;
using EventosVivos.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventosVivos.Infrastructure.Tests.Composition;

public class CompositionRootTests
{
    [Fact]
    public void Build_service_provider_succeeds_with_all_infrastructure_registrations()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=postgres",
                ["Jwt:Key"] = "test-key-must-be-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "Test",
                ["Jwt:Audience"] = "Test",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        services.AddApplication();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ReservationRuleSet>().Should().NotBeNull();
    }

    [Fact]
    public void ReservationRuleSet_resolves_with_four_ordered_rules()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=postgres",
                ["Jwt:Key"] = "test-key-must-be-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "Test",
                ["Jwt:Audience"] = "Test",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        services.AddApplication();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var ruleSet = provider.GetRequiredService<ReservationRuleSet>();

        ruleSet.Rules.Should().HaveCount(4);
        ruleSet.Rules.Select(r => r.GetType()).Should().ContainInOrder(
            typeof(LateReservationRule),
            typeof(Near24hRule),
            typeof(HighPriceRule),
            typeof(AvailabilityRule));
    }
}
