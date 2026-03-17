using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ProductCatalogue.Core.Entities;
using ProductCatalogue.Infrastructure.Persistence;

namespace ProductCatalogue.API.Extensions;

public static class DatabaseExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope  = app.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            // Use IRelationalDatabaseCreator so we can create tables even when
            // the SQLite file already exists (EnsureCreated returns false if the
            // file is present, even if it has no tables — this bypasses that).
            var creator = db.GetService<IRelationalDatabaseCreator>();

            if (!await creator.ExistsAsync())
                await creator.CreateAsync();

            if (!await creator.HasTablesAsync())
            {
                logger.LogInformation("Creating database schema...");
                await creator.CreateTablesAsync();
            }

            if (!await db.Products.AnyAsync())
            {
                logger.LogInformation("Seeding initial product data...");

                db.Products.AddRange(
                    Product.Create(
                        "Wireless Noise-Cancelling Headphones",
                        "Premium over-ear headphones with active noise cancellation, " +
                        "30-hour battery life, and foldable travel design."),
                    Product.Create(
                        "Mechanical Keyboard TKL",
                        "Tenkeyless layout with Cherry MX Brown switches, " +
                        "per-key RGB backlighting, and PBT doubleshot keycaps."),
                    Product.Create(
                        "USB-C 7-in-1 Hub",
                        "Multiport adapter with 4K HDMI, 3x USB-A 3.0, " +
                        "SD/microSD card reader, and 100W Power Delivery pass-through.")
                );

                await db.SaveChangesAsync();
                logger.LogInformation("Seed complete — 3 products added.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialisation or seed failed.");
            throw;
        }
    }
}
