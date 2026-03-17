using Microsoft.EntityFrameworkCore;
using ProductCatalogue.Core.Entities;

namespace ProductCatalogue.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Automatically discovers and applies all IEntityTypeConfiguration<T>
        // classes in this assembly, keeping DbContext clean.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
