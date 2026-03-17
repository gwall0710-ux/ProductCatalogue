using ProductCatalogue.Core.DTOs;

namespace ProductCatalogue.Core.Interfaces;

public interface IInventoryService
{
    Task<InventoryResult> GetInventoryAsync(int productId, CancellationToken ct = default);
}
