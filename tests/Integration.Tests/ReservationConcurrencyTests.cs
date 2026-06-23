using System.Net;
using System.Net.Http.Json;
using EventosVivos.Application.Events.CreateEvent;
using EventosVivos.Application.Reports.GetOccupancy;
using EventosVivos.Application.Reservations.CreateReservation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventosVivos.Integration.Tests;

public sealed class ReservationConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ReservationConcurrencyTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
        factory.ResetDatabaseAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Parallel_reservations_never_oversell()
    {
        // Arrange: event capacity = 10 (venue 1 capacity is 500)
        using var admin = await _factory.CreateAuthenticatedClientAsync("Admin");
        using var user = await _factory.CreateAuthenticatedClientAsync("User");

        var eventResponse = await admin.PostAsJsonAsync("/api/events", new
        {
            title = "Limited Seats Show",
            description = "Concurrency oversell proof event.",
            venueId = 1,
            capacity = 10,
            startUtc = new DateTime(2030, 9, 1, 20, 0, 0, DateTimeKind.Utc),
            endUtc = new DateTime(2030, 9, 1, 22, 0, 0, DateTimeKind.Utc),
            price = 100m,
            type = "Concierto"
        });
        eventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var evt = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(ApiFactory.JsonOptions);
        var eventId = evt!.Id;

        // Act: fire 50 concurrent reservations of qty 1
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => user.PostAsJsonAsync("/api/reservations", new
            {
                eventId,
                quantity = 1,
                buyerName = "x",
                buyerEmail = "x@y.com"
            }));

        var responses = await Task.WhenAll(tasks);

        // Assert: exactly 10 succeeded (201), rest rejected (422 / 409). Never >10 held.
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        successCount.Should().Be(10);

        foreach (var failed in responses.Where(r => !r.IsSuccessStatusCode))
        {
            failed.StatusCode.Should().BeOneOf(HttpStatusCode.UnprocessableEntity, HttpStatusCode.Conflict);
        }

        var occupancyResponse = await _client.GetAsync($"/api/events/{eventId}/occupancy");
        occupancyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var occ = await occupancyResponse.Content.ReadFromJsonAsync<OccupancyResponse>(ApiFactory.JsonOptions);

        // With PendingHoldsInventory=true, pending reservations hold seats.
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventosVivos.Infrastructure.Persistence.AppDbContext>();
        var pendingCount = await context.Reservations
            .Where(r => r.EventId == eventId && r.Status == EventosVivos.Domain.Enums.ReservationStatus.PendientePago)
            .SumAsync(r => r.Quantity);

        (occ!.SoldConfirmed + pendingCount).Should().BeLessThanOrEqualTo(10);
    }
}
