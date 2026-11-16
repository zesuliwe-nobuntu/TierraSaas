using System.ComponentModel.DataAnnotations;

namespace Tierra.Models;

public enum TenantRole
{
    Owner = 0,      // Business Customer Executive (Billing, invoices, subscriptions in ZAR)
    Admin = 1,      // Operational Workspace Manager (Team management, settings)
    EndUser = 2,    // Standard Business Employee / Customer (Clean workspace view, no billing)
    ITManager = 3   // Platform & Technical SuperAdmin (System health, Hangfire, DB metrics)
}

public class AppUser : IMustHaveTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public TenantRole Role { get; set; } = TenantRole.EndUser;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation property back to the Tenant organization
    public Tenant? Tenant { get; set; }
}
