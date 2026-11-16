using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tierra.Models;

public enum InvoiceStatus
{
    Paid = 0,
    Pending = 1,
    Failed = 2
}

public class Invoice : IMustHaveTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "ZAR"; // South African Rands

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(14);

    // Navigation property back to the Tenant
    public Tenant? Tenant { get; set; }
}