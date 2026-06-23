using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence;

public sealed class ConcurrencyRetryPolicy : IConcurrencyRetryPolicy
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> action, int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt >= maxAttempts)
                {
                    return Result.Failure<T>(new Error("concurrency.conflict", "The resource was modified by another request. Please retry."));
                }
            }
        }
    }
}
