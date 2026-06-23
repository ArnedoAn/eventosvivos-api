using EventosVivos.Domain.Enums;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Enums;

public class EnumTests
{
    [Fact]
    public void EventType_has_exact_members()
    {
        Enum.GetNames<EventType>()
            .Should()
            .BeEquivalentTo(new[] { "Conferencia", "Taller", "Concierto" });
    }

    [Fact]
    public void EventStatus_has_exact_members()
    {
        Enum.GetNames<EventStatus>()
            .Should()
            .BeEquivalentTo(new[] { "Activo", "Cancelado", "Completado" });
    }

    [Fact]
    public void ReservationStatus_has_exact_members()
    {
        Enum.GetNames<ReservationStatus>()
            .Should()
            .BeEquivalentTo(new[] { "PendientePago", "Confirmada", "Cancelada" });
    }
}
