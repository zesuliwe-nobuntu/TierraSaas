using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tierra.Models;

public enum SubscriptionTier
{
    Free = 0,
    Pro = 1,
    Enterprise = 2
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Canceled = 2
}

public class Subscription : IMustHaveTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyPrice { get; set; } = 0.00m;

    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;

    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);

    public DateTime? CanceledAt { get; set; }

    // Navigation property back to the Tenant
    public Tenant? Tenant { get; set; }
}
