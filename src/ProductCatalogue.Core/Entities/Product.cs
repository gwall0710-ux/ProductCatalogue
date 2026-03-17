namespace ProductCatalogue.Core.Entities;

public sealed class Product
{
    private Product() { } // Required by EF Core

    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Product Create(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Product
        {
            Name        = name.Trim(),
            Description = description.Trim(),
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
    }

    public void Update(string name, string description)
    {
        Name        = name.Trim();
        Description = description.Trim();
        UpdatedAt   = DateTime.UtcNow;
    }
}
