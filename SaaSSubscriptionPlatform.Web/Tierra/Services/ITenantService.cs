namespace Tierra.Services;

public interface ITenantService
{
    /// <summary>
    /// Returns the ID of the tenant making the current request, or null if unauthenticated.
    /// </summary>
    Guid? GetTenantId();

    /// <summary>
    /// Allows setting or overriding the tenant ID (essential for background jobs like Hangfire).
    /// </summary>
    void SetTenantId(Guid tenantId);
}