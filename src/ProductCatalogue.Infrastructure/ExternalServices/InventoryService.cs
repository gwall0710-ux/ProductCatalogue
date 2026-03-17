using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductCatalogue.Core.DTOs;
using ProductCatalogue.Core.Interfaces;

namespace ProductCatalogue.Infrastructure.ExternalServices;

public sealed class InventoryService(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache,
    IOptions<InventoryServiceOptions> options,
    ILogger<InventoryService> logger)
    : IInventoryService
{
    private const string CorrelationHeader = "X-Correlation-ID";
    private readonly InventoryServiceOptions _opts = options.Value;

    public async Task<InventoryResult> GetInventoryAsync(
        int productId, CancellationToken ct = default)
    {
        var cacheKey = $"inv:{productId}";

        if (cache.TryGetValue(cacheKey, out InventoryResult? cached))
        {
            logger.LogDebug("Cache hit for inventory product {ProductId}", productId);
            return cached!;
        }

        // Forward the Correlation ID so the outbound call is traceable
        var correlationId =
            httpContextAccessor.HttpContext?.Items[CorrelationHeader]?.ToString()
            ?? Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Remove(CorrelationHeader);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationHeader, correlationId);

        try
        {
            var response = await httpClient
                .GetFromJsonAsync<InventoryServiceResponse>($"inventory/{productId}", ct);

            if (response is null)
            {
                logger.LogWarning(
                    "Empty response from inventory provider for product {ProductId}", productId);
                return InventoryResult.Unavailable(
                    "Inventory provider returned an empty response.");
            }

            var result = InventoryResult.Success(
                response.Price, response.StockLevel, response.Currency);

            if (_opts.CacheTtlSeconds > 0)
                cache.Set(cacheKey, result, TimeSpan.FromSeconds(_opts.CacheTtlSeconds));

            return result;
        }
        catch (Exception ex)
            when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Inventory service unavailable for product {ProductId}", productId);

            return InventoryResult.Unavailable(
                "Real-time inventory data is currently unavailable. Please try again shortly.");
        }
    }
}
