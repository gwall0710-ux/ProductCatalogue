using Microsoft.EntityFrameworkCore;
using ProductCatalogue.Core.Entities;
using ProductCatalogue.Core.Interfaces;

namespace ProductCatalogue.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(AppDbContext context) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        => await context.Products.FindAsync([id], ct);

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => await context.Products
               .OrderBy(p => p.Name)
               .AsNoTracking()
               .ToListAsync(ct);

    public async Task<Product> AddAsync(Product product, CancellationToken ct = default)
    {
        await context.Products.AddAsync(product, ct);
        await context.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product?> UpdateAsync(
        int id, string name, string description, CancellationToken ct = default)
    {
        var product = await context.Products.FindAsync([id], ct);

        if (product is null)
            return null;

        product.Update(name, description);
        await context.SaveChangesAsync(ct);
        return product;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await context.Products.FindAsync([id], ct);

        if (product is null)
            return false;

        context.Products.Remove(product);
        await context.SaveChangesAsync(ct);
        return true;
    }
}
