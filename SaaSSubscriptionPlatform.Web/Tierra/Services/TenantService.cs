namespace Tierra.Services;

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantIdOverride;

    public const string TenantHeaderKey = "X-Tenant-Id";
    public const string TenantCookieKey = "Tierra_TenantId";

    public TenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetTenantId()
    {
        // 1. Manual override (used by Hangfire background workers)
        if (_tenantIdOverride.HasValue)
        {
            return _tenantIdOverride.Value;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        // 2. Authenticated User Claims (Tamper-proof!)
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = httpContext.User.FindFirst("TenantId");
            if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var claimTenantId))
            {
                return claimTenantId;
            }
        }

        // 3. Check HTTP Header (useful for REST API requests)
        if (httpContext.Request.Headers.TryGetValue(TenantHeaderKey, out var headerVal) 
            && Guid.TryParse(headerVal, out var headerTenantId))
        {
            return headerTenantId;
        }

        // 4. Check Browser Cookie (used for workspace switching / unauthenticated browsing)
        if (httpContext.Request.Cookies.TryGetValue(TenantCookieKey, out var cookieVal) 
            && Guid.TryParse(cookieVal, out var cookieTenantId))
        {
            return cookieTenantId;
        }

        return null;
    }

    public void SetTenantId(Guid tenantId)
    {
        _tenantIdOverride = tenantId;
    }
}