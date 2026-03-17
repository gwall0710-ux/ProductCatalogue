namespace ProductCatalogue.Infrastructure.ExternalServices;

// Internal-only model — maps the JSON shape returned by the inventory provider.
// Never expose this type outside the Infrastructure project.
internal sealed class InventoryServiceResponse
{
    public int     ProductId  { get; set; }
    public decimal Price      { get; set; }
    public int     StockLevel { get; set; }
    public string  Currency   { get; set; } = "GBP";
}
