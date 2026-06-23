using EventosVivos.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.BuyerName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Quantity).IsRequired();
        builder.Property(r => r.CreatedUtc).IsRequired();
        builder.Property(r => r.CancelledUtc);
        builder.Property(r => r.IsLost).IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.OwnsOne(r => r.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email_Value")
                .IsRequired()
                .HasMaxLength(256);
        });

        builder.OwnsOne(r => r.Code, code =>
        {
            code.Property(c => c.Value)
                .HasColumnName("Code_Value")
                .HasMaxLength(20)
                .IsRequired(false);

            code.HasIndex(c => c.Value)
                .IsUnique()
                .HasFilter("\"Code_Value\" IS NOT NULL");
        });

        builder.Navigation(r => r.Code).IsRequired(false);
    }
}
