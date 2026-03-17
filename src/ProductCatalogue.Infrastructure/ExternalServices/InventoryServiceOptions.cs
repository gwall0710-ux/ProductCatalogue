using System.ComponentModel.DataAnnotations;

namespace ProductCatalogue.Infrastructure.ExternalServices;

public sealed class InventoryServiceOptions
{
    public const string SectionName = "InventoryService";

    [Required, Url]
    public string BaseUrl { get; set; } = default!;

    [Range(1, 10)]
    public int RetryCount { get; set; } = 3;

    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 5;

    [Range(0, 3600)]
    public int CacheTtlSeconds { get; set; } = 30;
}
