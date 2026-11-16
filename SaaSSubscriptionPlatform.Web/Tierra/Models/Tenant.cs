using System.ComponentModel.DataAnnotations;

namespace Tierra.Models;

public class Tenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g. "Acme Corp"

    [Required]
    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty; // e.g. "acme" for acme.tierra.com

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation properties (Relationships)
    public Subscription? Subscription { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}