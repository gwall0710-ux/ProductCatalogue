namespace ProductCatalogue.Core.DTOs;

public enum InventoryStatus
{
    Live,
    Unavailable
}

public record InventoryResult
{
    public InventoryStatus Status     { get; init; }
    public decimal?        Price      { get; init; }
    public int?            StockLevel { get; init; }
    public string?         Currency   { get; init; }
    public string?         Warning    { get; init; }
    public DateTime?       FetchedAt  { get; init; }

    public static InventoryResult Success(decimal price, int stockLevel, string currency) =>
        new()
        {
            Status     = InventoryStatus.Live,
            Price      = price,
            StockLevel = stockLevel,
            Currency   = currency,
            FetchedAt  = DateTime.UtcNow
        };

    public static InventoryResult Unavailable(string reason) =>
        new()
        {
            Status  = InventoryStatus.Unavailable,
            Warning = reason
        };
}
