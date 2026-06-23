using EventosVivos.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", t => t.HasCheckConstraint("ck_event_capacity", "\"SeatsTaken\" + \"SeatsLost\" <= \"Capacity\""));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Capacity).IsRequired();
        builder.Property(e => e.SeatsTaken).IsRequired();
        builder.Property(e => e.SeatsLost).IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.OwnsOne(e => e.Schedule, schedule =>
        {
            schedule.Property(s => s.StartUtc).HasColumnName("Schedule_StartUtc");
            schedule.Property(s => s.EndUtc).HasColumnName("Schedule_EndUtc");
        });

        builder.OwnsOne(e => e.Price, price =>
        {
            price.Property(p => p.Amount)
                .HasColumnName("Price_Amount")
                .HasPrecision(18, 2);
        });
    }
}
