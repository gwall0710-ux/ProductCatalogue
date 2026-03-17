using ProductCatalogue.Core.Entities;

namespace ProductCatalogue.Core.DTOs;

public record EnrichedProductResponse
{
    public int      Id               { get; init; }
    public string   Name             { get; init; } = default!;
    public string   Description      { get; init; } = default!;
    public DateTime CreatedAt        { get; init; }

    // Inventory fields — null when service is unavailable
    public string?   InventoryStatus   { get; init; }  // "Live" | "Unavailable"
    public decimal?  Price             { get; init; }
    public string?   FormattedPrice    { get; init; }
    public int?      StockLevel        { get; init; }
    public string?   StockStatus       { get; init; }  // "InStock" | "LowStock" | "OutOfStock"
    public string?   Currency          { get; init; }
    public string?   InventoryWarning  { get; init; }
    public DateTime? InventoryFetchedAt { get; init; }

    public static EnrichedProductResponse From(Product product, InventoryResult inventory)
    {
        var stockStatus = inventory.StockLevel switch
        {
            null  => null,
            0     => "OutOfStock",
            <= 10 => "LowStock",
            _     => "InStock"
        };

        return new EnrichedProductResponse
        {
            Id                  = product.Id,
            Name                = product.Name,
            Description         = product.Description,
            CreatedAt           = product.CreatedAt,
            InventoryStatus     = inventory.Status.ToString(),
            Price               = inventory.Price,
            FormattedPrice      = inventory.Price.HasValue
                                    ? $"{inventory.Currency} {inventory.Price:F2}"
                                    : null,
            StockLevel          = inventory.StockLevel,
            StockStatus         = stockStatus,
            Currency            = inventory.Currency,
            InventoryWarning    = inventory.Warning,
            InventoryFetchedAt  = inventory.FetchedAt
        };
    }
}
