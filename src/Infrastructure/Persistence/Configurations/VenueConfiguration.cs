using EventosVivos.Domain.Venues;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("Venues");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.City).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Capacity).IsRequired();

        builder.HasData(
            new Venue(1, "Auditorio Central", 200, "Bogotá"),
            new Venue(2, "Sala Norte", 50, "Bogotá"),
            new Venue(3, "Arena Sur", 500, "Medellín"));
    }
}
