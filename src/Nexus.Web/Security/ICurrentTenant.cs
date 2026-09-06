using System.Security.Claims;

namespace Nexus.Web.Security;

/// <summary>
/// Resolves the current request's OrganizationId from the authenticated
/// user's claims. This is the single source of truth EF Core's global query
/// filters key off (see NexusDbContext.OnModelCreating) so tenant isolation
/// is enforced at the data-access layer, not just by controllers remembering
/// to filter (§22: "never rely only on UI filtering for tenant security").
/// </summary>
public interface ICurrentTenant
{
    Guid? OrganizationId { get; }
    bool IsResolved { get; }
}

public class HttpContextCurrentTenant : ICurrentTenant
{
    // Ambient override for code paths with no HttpContext (background jobs,
    // hosted services). AsyncLocal flows correctly across await boundaries
    // within the same logical call chain, so a background worker can wrap a
    // job's processing in UseTenant(...) and every DI-resolved
    // NexusDbContext created inside that block - however many services deep
    // - sees the right tenant, without having to manually construct
    // DbContext instances or thread OrganizationId through every service
    // constructor. This was the missing piece that made the original §46
    // background worker silently read zero rows: it resolved NexusDbContext
    // via DI inside a hosted-service scope, which has no HttpContext, so
    // OrganizationId resolved to null and every filtered read failed closed.
    private static readonly AsyncLocal<Guid?> _ambientOverride = new();

    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentTenant(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? OrganizationId
    {
        get
        {
            if (_ambientOverride.Value is Guid ambient) return ambient;
            var claim = _accessor.HttpContext?.User?.FindFirst("org_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsResolved => OrganizationId is not null;

    /// <summary>
    /// Sets the ambient tenant for the duration of the returned scope, for
    /// use by background workers and other non-HTTP call paths. Always call
    /// via `using` so the override is cleared afterward, and always pass an
    /// OrganizationId you have independently verified the caller is
    /// authorized to act as - this bypasses claim-based resolution entirely,
    /// so misuse would be a tenant-isolation bypass.
    /// </summary>
    public static IDisposable UseTenant(Guid organizationId)
    {
        var previous = _ambientOverride.Value;
        _ambientOverride.Value = organizationId;
        return new AmbientScope(() => _ambientOverride.Value = previous);
    }

    private sealed class AmbientScope : IDisposable
    {
        private readonly Action _onDispose;
        public AmbientScope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}

/// <summary>
/// Used by background jobs / seeders / dev bootstrap that run outside an
/// HTTP request and therefore have no user claims to read. Must be set
/// explicitly - it does not fall back to "no filter" silently, which would
/// be a tenant-isolation bypass.
/// </summary>
public class FixedCurrentTenant : ICurrentTenant
{
    public FixedCurrentTenant(Guid organizationId) => OrganizationId = organizationId;
    public Guid? OrganizationId { get; }
    public bool IsResolved => true;
}
