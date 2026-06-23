using EventosVivos.Domain.Common;

namespace EventosVivos.Application.Abstractions;

public interface IConcurrencyRetryPolicy
{
    Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> action, int maxAttempts = 3);
}
