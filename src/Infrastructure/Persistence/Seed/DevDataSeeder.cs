using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Seed;

public sealed class DevDataSeeder
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Email == "admin@eventosvivos.com", ct))
        {
            context.Users.Add(new AppUser
            {
                Id = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"),
                Email = "admin@eventosvivos.com",
                PasswordHash = SeedPasswords.AdminHash,
                Role = Role.Admin
            });
        }

        if (!await context.Users.AnyAsync(u => u.Email == "user@eventosvivos.com", ct))
        {
            context.Users.Add(new AppUser
            {
                Id = Guid.Parse("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"),
                Email = "user@eventosvivos.com",
                PasswordHash = SeedPasswords.UserHash,
                Role = Role.User
            });
        }

        await context.SaveChangesAsync(ct);
    }
}
