namespace Tierra.Models;

/// <summary>
/// A contract for any database entity that belongs to a specific tenant.
/// EF Core will look for this interface to automatically isolate data.
/// </summary>
public interface IMustHaveTenant
{
    public Guid TenantId { get; set; }
}