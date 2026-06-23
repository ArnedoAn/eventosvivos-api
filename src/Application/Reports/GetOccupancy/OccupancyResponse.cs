using EventosVivos.Domain.Enums;

namespace EventosVivos.Application.Reports.GetOccupancy;

public sealed record OccupancyResponse(
    Guid EventId,
    string Title,
    int Capacity,
    int SoldConfirmed,
    int AvailableRemaining,
    int RetainedByPenalty,
    double OccupancyPercent,
    decimal TotalRevenue,
    EventStatus Status);
