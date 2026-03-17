using ProductCatalogue.Core.Entities;

namespace ProductCatalogue.Core.DTOs;

public record ProductResponse(
    int      Id,
    string   Name,
    string   Description,
    DateTime CreatedAt)
{
    public static ProductResponse From(Product p) =>
        new(p.Id, p.Name, p.Description, p.CreatedAt);
}
