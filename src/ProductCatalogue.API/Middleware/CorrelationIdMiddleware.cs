using Serilog.Context;

namespace ProductCatalogue.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers[Header].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // Store so InventoryService can forward it on outbound HTTP calls
        context.Items[Header] = correlationId;

        // Echo back so the caller can correlate their request with logs
        context.Response.Headers[Header] = correlationId;

        // Push into Serilog LogContext — every log line in this request
        // will now automatically include the CorrelationId property
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
