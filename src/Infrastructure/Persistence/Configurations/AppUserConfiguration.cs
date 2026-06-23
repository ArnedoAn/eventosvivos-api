using EventosVivos.Domain.Users;
using EventosVivos.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Email).IsUnique();

        // Deterministic seed hashes. Override Seed:AdminPasswordHash / Seed:UserPasswordHash
        // in production (and run a new migration) so these dev credentials are not deployed.
        builder.HasData(
            new AppUser
            {
                Id = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"),
                Email = "admin@eventosvivos.com",
                PasswordHash = SeedPasswords.AdminHash,
                Role = Role.Admin
            },
            new AppUser
            {
                Id = Guid.Parse("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"),
                Email = "user@eventosvivos.com",
                PasswordHash = SeedPasswords.UserHash,
                Role = Role.User
            });
    }
}
