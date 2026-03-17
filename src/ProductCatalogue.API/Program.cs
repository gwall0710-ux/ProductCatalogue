using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductCatalogue.API.Extensions;
using ProductCatalogue.API.Infrastructure;
using ProductCatalogue.API.Middleware;
using ProductCatalogue.Infrastructure.DependencyInjection;
using ProductCatalogue.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog structured logging ────────────────────────────────────────────────
// The output template includes {CorrelationId} which is populated by
// CorrelationIdMiddleware pushing it into Serilog's LogContext.
// Every log line in a request will carry the same Correlation ID.
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

// ── Infrastructure: DB, InventoryService, Polly, cache ────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── API services ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Allow numeric values in fields declared as strings (e.g. "name": 5 → "5").
        // Without this, passing an integer where a string is expected causes a hard
        // deserialisation failure that bypasses all validation logic.
        opts.JsonSerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Override the default 400 response from model binding failures so that
        // JSON deserialisation errors (e.g. passing an int where a string is expected)
        // return the same 422 ProblemDetails shape as our FluentValidation errors,
        // rather than ASP.NET Core's built-in 400 format.
        options.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

            var problem = new ProblemDetails
            {
                Title  = "One or more validation errors occurred.",
                Status = StatusCodes.Status422UnprocessableEntity,
                Extensions = { ["errors"] = errors }
            };

            return new UnprocessableEntityObjectResult(problem);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Product Catalogue API",
        Version     = "v1",
        Description = "Manages a product catalogue and enriches product data " +
                      "with real-time inventory information from a third-party provider."
    });

    // Adds X-Correlation-ID as an optional header field on every endpoint
    c.OperationFilter<CorrelationIdOperationFilter>();
});

// ── FluentValidation ──────────────────────────────────────────────────────────
// Registers all AbstractValidator<T> classes in this assembly automatically
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database")
    .AddUrlGroup(
        new Uri(
            (builder.Configuration["InventoryService:BaseUrl"]
             ?? "http://localhost:5000/mock/")
            + "inventory/health"),
        name:          "inventory-service",
        failureStatus: HealthStatus.Degraded);

// ── Global exception handler (RFC 7807 ProblemDetails) ────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Apply EF Core migrations and seed data on startup ────────────────────────
await app.InitialiseDatabaseAsync();

// ── Middleware pipeline — ORDER MATTERS ───────────────────────────────────────
app.UseExceptionHandler();                      // must be first to catch all errors
app.UseMiddleware<CorrelationIdMiddleware>();    // early — Serilog scope depends on it
app.UseSerilogRequestLogging();                 // logs method, path, status, duration

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Catalogue v1"));
}

app.MapControllers();

// Custom health check response writer — returns JSON with per-check detail
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            status        = report.Status.ToString(),
            checks        = report.Entries.Select(e => new
            {
                name     = e.Key,
                status   = e.Value.Status.ToString(),
                duration = $"{e.Value.Duration.TotalMilliseconds:F1}ms"
            }),
            totalDuration = $"{report.TotalDuration.TotalMilliseconds:F1}ms"
        }));
    }
});

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
