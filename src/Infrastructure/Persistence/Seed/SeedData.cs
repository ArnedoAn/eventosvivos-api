using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Seed;

public static class SeedData
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        // Venue and user seed data is configured in their respective IEntityTypeConfiguration classes.
        // This method is the central hook called from AppDbContext.OnModelCreating.
    }
}
