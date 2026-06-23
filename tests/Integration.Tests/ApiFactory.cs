using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventosVivos.Application.Auth.Login;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventosVivos.Integration.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("eventosvivos_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Jwt:Key"] = "integration-test-key-must-be-at-least-32-bytes-long!",
                ["Jwt:Issuer"] = "EventosVivos.Tests",
                ["Jwt:Audience"] = "EventosVivos.Tests",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Reservation:PendingHoldsInventory"] = "true",
                ["Reservation:PendingExpirationMinutes"] = "0"
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Client = CreateClient();
    }

    public new async Task DisposeAsync()
    {
        Client.Dispose();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task<string> GetTokenAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return result!.Token;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string role = "User")
    {
        var email = role == "Admin" ? "admin@eventosvivos.com" : "user@eventosvivos.com";
        var password = role == "Admin" ? "Admin123!" : "User123!";

        var token = await GetTokenAsync(email, password);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventosVivos.Infrastructure.Persistence.AppDbContext>();
        await context.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
        await context.Database.MigrateAsync();
    }

    public static JsonSerializerOptions JsonOptions => new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
