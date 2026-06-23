using System.Net;
using System.Net.Http.Json;
using EventosVivos.Application.Auth.Login;
using EventosVivos.Application.Events.CreateEvent;
using EventosVivos.Application.Reports.GetOccupancy;
using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Enums;
using FluentAssertions;

namespace EventosVivos.Integration.Tests;

public sealed class EndpointSmokeTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EndpointSmokeTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_and_role()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@eventosvivos.com",
            password = "Admin123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(ApiFactory.JsonOptions);
        result!.Role.Should().Be("Admin");
        result.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_invalid_credentials_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@eventosvivos.com",
            password = "wrong"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_event_as_admin_returns_201()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync("Admin");
        var response = await admin.PostAsJsonAsync("/api/events", new
        {
            title = "Rock Concert",
            description = "A great live rock show in the city.",
            venueId = 1,
            capacity = 100,
            startUtc = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            endUtc = new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
            price = 50m,
            type = "Concierto"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var evt = await response.Content.ReadFromJsonAsync<EventResponse>(ApiFactory.JsonOptions);
        evt!.Title.Should().Be("Rock Concert");
        evt.Status.Should().Be(EventStatus.Activo);
    }

    [Fact]
    public async Task Create_event_as_user_returns_403()
    {
        using var user = await _factory.CreateAuthenticatedClientAsync("User");
        var response = await user.PostAsJsonAsync("/api/events", new
        {
            title = "Rock Concert",
            description = "A great live rock show in the city.",
            venueId = 1,
            capacity = 100,
            startUtc = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            endUtc = new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
            price = 50m,
            type = "Concierto"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_events_returns_events()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync("Admin");
        await admin.PostAsJsonAsync("/api/events", new
        {
            title = "Jazz Night",
            description = "An evening of smooth jazz.",
            venueId = 2,
            capacity = 50,
            startUtc = new DateTime(2030, 7, 10, 20, 0, 0, DateTimeKind.Utc),
            endUtc = new DateTime(2030, 7, 10, 22, 0, 0, DateTimeKind.Utc),
            price = 30m,
            type = "Concierto"
        });

        var response = await _client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>(ApiFactory.JsonOptions);
        events.Should().Contain(e => e.Title == "Jazz Night");
    }

    [Fact]
    public async Task Reserve_confirm_cancel_and_occupancy_flow_works()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync("Admin");
        using var user = await _factory.CreateAuthenticatedClientAsync("User");

        var eventResponse = await admin.PostAsJsonAsync("/api/events", new
        {
            title = "Theatre Play",
            description = "A classic theatre performance.",
            venueId = 3,
            capacity = 10,
            startUtc = new DateTime(2030, 8, 20, 19, 0, 0, DateTimeKind.Utc),
            endUtc = new DateTime(2030, 8, 20, 21, 0, 0, DateTimeKind.Utc),
            price = 75m,
            type = "Teatro"
        });
        eventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var evt = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(ApiFactory.JsonOptions);
        var eventId = evt!.Id;

        var reserveResponse = await user.PostAsJsonAsync("/api/reservations", new
        {
            eventId,
            quantity = 2,
            buyerName = "Alice Smith",
            buyerEmail = "alice@example.com"
        });
        reserveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservation = await reserveResponse.Content.ReadFromJsonAsync<ReservationResponse>(ApiFactory.JsonOptions);
        reservation!.Quantity.Should().Be(2);
        reservation.Status.Should().Be(ReservationStatus.PendientePago);

        var occupancyBefore = await _client.GetAsync($"/api/events/{eventId}/occupancy");
        occupancyBefore.StatusCode.Should().Be(HttpStatusCode.OK);
        var occBefore = await occupancyBefore.Content.ReadFromJsonAsync<OccupancyResponse>(ApiFactory.JsonOptions);
        occBefore!.AvailableRemaining.Should().Be(8);

        var confirmResponse = await admin.PostAsJsonAsync($"/api/reservations/{reservation.Id}/confirm", new { });
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<ReservationResponse>(ApiFactory.JsonOptions);
        confirmed!.Status.Should().Be(ReservationStatus.Confirmada);

        var occupancyAfter = await _client.GetAsync($"/api/events/{eventId}/occupancy");
        occupancyAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var occAfter = await occupancyAfter.Content.ReadFromJsonAsync<OccupancyResponse>(ApiFactory.JsonOptions);
        occAfter!.SoldConfirmed.Should().Be(2);
        occAfter.AvailableRemaining.Should().Be(8);

        var cancelResponse = await user.PostAsJsonAsync($"/api/reservations/{reservation.Id}/cancel", new { });
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<ReservationResponse>(ApiFactory.JsonOptions);
        cancelled!.Status.Should().Be(ReservationStatus.Cancelada);

        var occupancyFinal = await _client.GetAsync($"/api/events/{eventId}/occupancy");
        occupancyFinal.StatusCode.Should().Be(HttpStatusCode.OK);
        var occFinal = await occupancyFinal.Content.ReadFromJsonAsync<OccupancyResponse>(ApiFactory.JsonOptions);
        occFinal!.SoldConfirmed.Should().Be(0);
        occFinal.RetainedByPenalty.Should().Be(2);
    }
}
