using System.Reflection;
using EventosVivos.Application.Behaviors;
using EventosVivos.Domain.Rules;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventosVivos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var domainAssembly = typeof(IReservationRule).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<ReservationRuleSet>();
        services.AddScoped<IReservationRule, LateReservationRule>();
        services.AddScoped<IReservationRule, Near24hRule>();
        services.AddScoped<IReservationRule, HighPriceRule>();
        services.AddScoped<IReservationRule, AvailabilityRule>();

        return services;
    }
}
