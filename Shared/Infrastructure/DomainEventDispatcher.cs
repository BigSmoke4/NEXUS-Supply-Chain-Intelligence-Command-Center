using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NEXUS.Shared.Kernel;

namespace NEXUS.Shared.Infrastructure;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(ILogger<DomainEventDispatcher> logger)
    {
        _logger = logger;
    }

    public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            _logger.LogInformation(
                "Dispatched domain event {EventType} ({EventId}) at {OccurredAtUtc}",
                domainEvent.EventType,
                domainEvent.EventId,
                domainEvent.OccurredAtUtc);
        }

        return Task.CompletedTask;
    }
}

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        await _next(context);
    }
}
