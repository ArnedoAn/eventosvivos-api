namespace EventosVivos.Domain.Venues;

/// <summary>
/// Reference-data record for a physical venue where events are held.
/// </summary>
public sealed record Venue(int Id, string Name, int Capacity, string City);
