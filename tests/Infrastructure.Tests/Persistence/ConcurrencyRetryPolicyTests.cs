using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Tests.Persistence;

public class ConcurrencyRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_succeeds_when_action_succeeds_immediately()
    {
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy();

        var result = await policy.ExecuteAsync(() => Task.FromResult(Result.Success(42)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_retries_on_concurrency_exception_then_succeeds()
    {
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy();
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
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_after_max_attempts()
    {
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy();
        var attempts = 0;

        var result = await policy.ExecuteAsync<int>(() =>
        {
            attempts++;
            throw new DbUpdateConcurrencyException("conflict");
        }, maxAttempts: 2);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("concurrency.conflict");
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_retry_on_regular_exception()
    {
        IConcurrencyRetryPolicy policy = new ConcurrencyRetryPolicy();
        Func<Task<Result<int>>> action = () => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(action, maxAttempts: 3));
    }
}
