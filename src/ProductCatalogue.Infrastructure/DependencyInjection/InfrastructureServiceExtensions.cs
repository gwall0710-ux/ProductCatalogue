using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProductCatalogue.Core.Interfaces;
using ProductCatalogue.Infrastructure.ExternalServices;
using ProductCatalogue.Infrastructure.Persistence;
using ProductCatalogue.Infrastructure.Persistence.Repositories;

namespace ProductCatalogue.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite(
                configuration.GetConnectionString("Default")
                ?? "Data Source=products.db"));

        services.AddScoped<IProductRepository, ProductRepository>();

        // ── Inventory service options (validated at startup) ──────────────────
        services
            .AddOptions<InventoryServiceOptions>()
            .Bind(configuration.GetSection(InventoryServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ── HttpContextAccessor so InventoryService can read Correlation ID ────
        services.AddHttpContextAccessor();

        // ── InventoryService HTTP client with Polly standard resilience pipeline ─
        // AddStandardResilienceHandler wires:
        //   - Per-attempt timeout
        //   - Retry with exponential backoff + jitter (prevents thundering herd)
        //   - Circuit breaker (opens after sustained failures, giving downstream a break)
        services
            .AddHttpClient<IInventoryService, InventoryService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<InventoryServiceOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResilienceHandler(opts =>
            {
                opts.Retry.MaxRetryAttempts = 3;
                opts.Retry.UseJitter        = true;
                opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });

        // ── In-memory cache for inventory responses ────────────────────────────
        services.AddMemoryCache();

        return services;
    }
}
