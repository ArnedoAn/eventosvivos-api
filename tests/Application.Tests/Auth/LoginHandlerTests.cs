using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Auth.Login;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.Venues;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Tests.Auth;

public class LoginHandlerTests
{
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string Generate(AppUser user) => $"token-for-{user.Id}";
    }

    private sealed class TestAppDbContext : DbContext, IAppDbContext
    {
        public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

        public DbSet<Event> Events => Set<Event>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<Venue> Venues => Set<Venue>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public void ResetChangeTracker() => ChangeTracker.Clear();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Event>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Schedule);
                e.OwnsOne(x => x.Price);
            });
            modelBuilder.Entity<Venue>(e => e.HasKey(x => x.Id));
            modelBuilder.Entity<AppUser>(e => e.HasKey(x => x.Id));
            modelBuilder.Entity<Reservation>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Email);
                e.OwnsOne(x => x.Code);
            });
        }
    }

    private static TestAppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private static LoginHandler CreateHandler(IAppDbContext db, IPasswordHasher? hasher = null, IJwtTokenService? jwt = null)
        => new(db, hasher ?? new FakePasswordHasher(), jwt ?? new FakeJwtTokenService());

    [Fact]
    public async Task Valid_credentials_returns_token_and_role()
    {
        await using var db = CreateDb();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash:password",
            Role = Role.User
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new LoginCommand("user@example.com", "password"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be($"token-for-{user.Id}");
        result.Value.Role.Should().Be("User");
    }

    [Fact]
    public async Task Wrong_password_returns_invalid_credentials_error()
    {
        await using var db = CreateDb();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash:password",
            Role = Role.User
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new LoginCommand("user@example.com", "wrong"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalidCredentials");
    }

    [Fact]
    public async Task Unknown_email_returns_invalid_credentials_error()
    {
        await using var db = CreateDb();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new LoginCommand("missing@example.com", "password"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.invalidCredentials");
    }
}
