using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.Venues;
using EventosVivos.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Tests.Persistence;

public class ConcurrencyRetryPolicyTests
{
    private sealed class StubDbContext : IAppDbContext
    {
        public int ResetCalls { get; private set; }

        public DbSet<Event> Events => throw new NotSupportedException();
        public DbSet<Reservation> Reservations => throw new NotSupportedException();
        public DbSet<Venue> Venues => throw new NotSupportedException();
        public DbSet<AppUser> Users => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();

        public void ResetChangeTracker() => ResetCalls++;
    }

    [Fact]
    public async Task ExecuteAsync_succeeds_when_action_succeeds_immediately()
    {
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy(new StubDbContext());

        var result = await policy.ExecuteAsync(() => Task.FromResult(Result.Success(42)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_retries_on_concurrency_exception_then_succeeds()
    {
        var context = new StubDbContext();
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy(context);
        var attempts = 0;

        var result = await policy.ExecuteAsync<int>(() =>
        {
            attempts++;
            if (attempts < 3)
                throw new DbUpdateConcurrencyException("conflict");

            return Task.FromResult(Result.Success(7));
        }, maxAttempts: 3);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7);
        attempts.Should().Be(3);
        context.ResetCalls.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_after_max_attempts()
    {
        var context = new StubDbContext();
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy(context);
        var attempts = 0;

        var result = await policy.ExecuteAsync<int>(() =>
        {
            attempts++;
            throw new DbUpdateConcurrencyException("conflict");
        }, maxAttempts: 2);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("concurrency.conflict");
        attempts.Should().Be(2);
        context.ResetCalls.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_retry_on_regular_exception()
    {
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy(new StubDbContext());
        Func<Task<Result<int>>> action = () => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(action, maxAttempts: 3));
    }
}
