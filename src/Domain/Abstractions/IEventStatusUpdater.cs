namespace EventosVivos.Domain.Abstractions;

/// <summary>
/// Seam for updating event statuses that have reached their end time.
/// </summary>
/// <remarks>
/// Decision #3: the chosen implementation is on-read auto-completion via
/// <see cref="Events.Event.RefreshStatus(DateTime)"/> invoked by query handlers.
/// A hosted background job is a valid alternative that would periodically call
/// <see cref="MarkCompletedEventsAsync(CancellationToken)"/>, but it is left as a
/// trace and not implemented here.
/// </remarks>
public interface IEventStatusUpdater
{
    Task MarkCompletedEventsAsync(CancellationToken cancellationToken);
}
