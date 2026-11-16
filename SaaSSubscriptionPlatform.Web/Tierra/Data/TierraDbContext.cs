using Microsoft.EntityFrameworkCore;
using Tierra.Models;
using Tierra.Services;

namespace Tierra.Data;

public class TierraDbContext : DbContext
{
    private readonly ITenantService _tenantService;
    public Guid? CurrentTenantId => _tenantService.GetTenantId();

    public TierraDbContext(DbContextOptions<TierraDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Global Query Filter for Multi-Tenancy
        // If CurrentTenantId is null (e.g. background job running migrations), show all.
        // If inside a tenant workspace, strictly filter records by TenantId.
        modelBuilder.Entity<Subscription>()
            .HasQueryFilter(s => CurrentTenantId == null || s.TenantId == CurrentTenantId);

        modelBuilder.Entity<Invoice>()
            .HasQueryFilter(i => CurrentTenantId == null || i.TenantId == CurrentTenantId);

        modelBuilder.Entity<AppUser>()
            .HasQueryFilter(u => CurrentTenantId == null || u.TenantId == CurrentTenantId);

        // 2. Ensure Tenant Slug is unique (no two companies can have the same slug)
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        // 3. Ensure Email is unique per tenant
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 3. Automated Tenant Stamping
        // Automatically inject the current TenantId on all new records implementing IMustHaveTenant
        if (CurrentTenantId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries<IMustHaveTenant>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = CurrentTenantId.Value;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}