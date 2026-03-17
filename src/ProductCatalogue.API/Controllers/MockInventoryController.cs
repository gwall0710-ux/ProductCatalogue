using Microsoft.AspNetCore.Mvc;

namespace ProductCatalogue.API.Controllers;

/// <summary>
/// Simulates a third-party inventory provider API.
///
/// InventoryService calls this endpoint via HttpClientFactory using the BaseUrl
/// from appsettings.json, which points to this same process. This means the full
/// HttpClient pipeline — Polly retry, circuit breaker, Correlation ID forwarding —
/// is exercised against a real HTTP request over loopback.
///
/// To switch to a real provider: change InventoryService:BaseUrl in appsettings.json.
/// No code changes required.
/// </summary>
[ApiController]
[Route("mock")]
[Tags("Mock Inventory Provider")]
public sealed class MockInventoryController : ControllerBase
{
    // Deterministic failure simulation:
    // Every BURST_SIZE consecutive requests to this endpoint will fail,
    // followed by SUCCEED_SIZE successful requests, then repeat.
    // This guarantees Polly's retries all land within a failure window,
    // making the fallback reliably observable.
    private static int _requestCount = 0;
    private const int FailEveryN  = 4;  // fail 1 out of every N requests to the mock
    private const int BurstSize   = 4;  // how many consecutive failures per burst
                                        // (must be > Polly retry count of 3 to exhaust all retries)

    /// <summary>
    /// Returns simulated price and stock for a product.
    /// Fails in bursts of 4 consecutive requests so Polly's retry pipeline
    /// is exhausted and the fallback response is reliably triggered.
    /// </summary>
    [HttpGet("inventory/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetInventory(int id)
    {
        var count = Interlocked.Increment(ref _requestCount);

        // Fail requests 1–4 of every FailEveryN * BurstSize cycle.
        // e.g. with FailEveryN=4 and BurstSize=4: requests 1–4 fail, 5–16 succeed,
        // 17–20 fail, 21–32 succeed, etc.
        var positionInCycle = (count - 1) % (FailEveryN * BurstSize);
        var shouldFail = positionInCycle < BurstSize;

        if (shouldFail)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"Simulated inventory provider outage. (request {count} in cycle)" });
        }

        var price      = Math.Round(Random.Shared.NextDouble() * 499 + 0.99, 2);
        var stockLevel = Random.Shared.Next(0, 500);

        return Ok(new
        {
            ProductId  = id,
            Price      = (decimal)price,
            StockLevel = stockLevel,
            Currency   = "GBP"
        });
    }

    /// <summary>Health endpoint used by the API health check.</summary>
    [HttpGet("inventory/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
