# Product Catalogue API

A .NET 8 Web API that manages a product catalogue and enriches product data with real-time inventory information from a third-party provider.

## Tech stack

| Concern | Library |
|---|---|
| Framework | .NET 8 · ASP.NET Core Web API |
| ORM | Entity Framework Core 8 · SQLite |
| HTTP client | HttpClientFactory |
| Resilience | Polly v8 via `Microsoft.Extensions.Http.Resilience` |
| Validation | FluentValidation 11 |
| Logging | Serilog with structured output |
| API docs | Swagger / Swashbuckle |
| Tests | xUnit · NSubstitute · FluentAssertions |

---

## Running the solution

```bash
cd src/ProductCatalogue.API
dotnet run
```

The mock inventory endpoint (`/mock/inventory/{id}`) is built into the same API project — only **one process** needs to run.

| URL | Description |
|---|---|
| `http://localhost:5000/swagger` | Swagger UI — try all endpoints here |
| `http://localhost:5000/api/products` | Product list |
| `http://localhost:5000/api/products/1` | Single product with live inventory data |
| `http://localhost:5000/health` | Health check (DB + inventory service) |

The SQLite database (`products.db`) is created and seeded with 3 products automatically on first run. No setup required.

To reset the database, stop the API and delete `src/ProductCatalogue.API/products.db`. It will be recreated and reseeded on next run.

---

## Running the tests

```bash
cd tests/ProductCatalogue.Tests
dotnet test
```

10 tests (14 including theory cases) covering: enriched happy path, inventory fallback, 404 guard, 422 validation, stock status derivation, update happy path, update not found, update validation, delete success, and delete not found.

---

## Architecture

Three projects with strict one-way dependency rules:

```
ProductCatalogue.Core          (zero external NuGet dependencies)
       ↑
ProductCatalogue.Infrastructure  (EF Core, HttpClient, Polly, Cache)
       ↑
ProductCatalogue.API             (Controllers, Middleware, DI composition root)
       ↑
ProductCatalogue.Tests           (references all three — test only)
```

`Core` defines the domain: entities, interfaces, and DTOs. `Infrastructure` implements those interfaces. `API` wires everything together at the composition root. Nothing ever references upward through the layers.

---

## API endpoints

| Method | Route | Description | Notes |
|---|---|---|---|
| `GET` | `/api/products` | All products | Local data only |
| `GET` | `/api/products/{id}` | Single product | Enriched with live inventory |
| `POST` | `/api/products` | Create product | Local only, validated |
| `PUT` | `/api/products/{id}` | Update product | Local only, validated |
| `DELETE` | `/api/products/{id}` | Delete product | Returns 204 No Content |
| `GET` | `/mock/inventory/{id}` | Simulated third-party provider | Built-in mock |
| `GET` | `/health` | Health check | DB + inventory service status |

A `requests.http` file is included at the solution root with ready-to-run examples for every endpoint, including validation failure cases.

---

## Key design decisions

### Mock inventory in the same project
`MockInventoryController` lives at `/mock/inventory/{id}` inside the API project. `InventoryService` calls it via `HttpClientFactory` over loopback HTTP — identical in behaviour to calling a real third-party URL. The full Polly pipeline (retry, circuit breaker, timeout) fires on a genuine HTTP request over a real network boundary. Switching to a live provider requires only changing `InventoryService:BaseUrl` in `appsettings.json` — zero code changes.

The mock uses a deterministic burst counter rather than a random failure rate. Every 16th call to the mock endpoint begins a burst of 4 consecutive failures, which is enough to exhaust Polly's 3 retries and trigger the fallback response. This makes the resilience behaviour reliably observable during a demo without relying on probability.

### Polly via `AddStandardResilienceHandler`
The standard resilience handler configures per-attempt timeout, retry with exponential backoff and jitter, and circuit breaker in a single call. Jitter prevents thundering herd on retry storms. All thresholds are set in the options lambda in `InfrastructureServiceExtensions` and can be adjusted via `appsettings.json` without code changes.

### Fallback via `InventoryResult`
`InventoryService` never throws for network failures. It catches `HttpRequestException` and `TaskCanceledException` and returns `InventoryResult.Unavailable(reason)` instead. `ProductsController` always returns `200` — the response carries `inventoryStatus: "Unavailable"` and a human-readable `inventoryWarning` when the provider is unreachable. The product's local data is always present regardless of inventory state.

### Consistent validation error responses
Both model binding failures (wrong JSON types) and FluentValidation failures return the same `422 Unprocessable Entity` shape with an `errors` object. `InvalidModelStateResponseFactory` is overridden in `Program.cs` to intercept ASP.NET Core's default `400` response and reformat it to match. `JsonNumberHandling.AllowReadingFromString` is also enabled so sending `"name": 5` coerces to the string `"5"` and proceeds through FluentValidation rather than hard-failing at deserialisation.

### Correlation ID end-to-end
`CorrelationIdMiddleware` reads or generates `X-Correlation-ID` on every inbound request and stores it in `HttpContext.Items`. Serilog's `LogContext` receives it so every log line in that request carries the same ID. `InventoryService` reads it via `IHttpContextAccessor` and forwards it as a header on the outbound HTTP call — the mock endpoint receives the same ID, making the full request traceable across both inbound and outbound legs.

### Strongly-typed options with startup validation
`InventoryServiceOptions` uses `[Required]` and `[Url]` data annotations with `ValidateOnStart()`. A misconfigured `BaseUrl` crashes immediately at startup with a clear error rather than failing silently on the first real request.

### `IEntityTypeConfiguration<T>` for EF Core
Entity mappings live in `ProductConfiguration` rather than `OnModelCreating`. This keeps `AppDbContext` clean and each entity's mapping self-contained and independently maintainable.

### Cache disabled in Development
`CacheTtlSeconds: 0` in `appsettings.Development.json` disables inventory caching in the development environment so every request hits the mock and resilience behaviour is observable. The production `appsettings.json` sets a 30-second TTL, reducing load on the third-party provider in real deployments.

---

## What would be added with more time

- **DelegatingHandler for Correlation ID** — rather than `IHttpContextAccessor` in `InventoryService`, a `DelegatingHandler` in the `HttpClient` pipeline would forward the header transparently on every outbound request, decoupling the service from ASP.NET Core entirely and working correctly in background jobs where `HttpContext` is null

- **JWT authentication** — `AddAuthentication()` + `AddJwtBearer()`, protecting write endpoints (`POST`, `PUT`, `DELETE`) with `[Authorize]`

- **Distributed cache** — swap `IMemoryCache` for `IDistributedCache` backed by Redis; cache sharing across multiple API instances with zero changes to `InventoryService` (depends only on the interface)

- **Integration tests** — `WebApplicationFactory<Program>` with `WireMock.Net` stubbing the inventory endpoint, testing the full HTTP stack end-to-end including middleware behaviour, Polly retry, and the correlation ID flowing through both request legs

- **CQRS via MediatR** — decompose controller logic into Commands and Queries with pipeline behaviours for validation and logging, removing the validator injection from the controller and making cross-cutting concerns fully transparent

- **docker-compose** — single-command startup for API + Redis + Jaeger

- **OpenTelemetry** — distributed tracing with `AddOpenTelemetry()` and OTLP export to Jaeger or Grafana, replacing the current Serilog-only observability
