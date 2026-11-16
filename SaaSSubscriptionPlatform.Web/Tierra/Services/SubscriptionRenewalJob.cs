using Microsoft.EntityFrameworkCore;
using Tierra.Data;
using Tierra.Models;

namespace Tierra.Services;

public class SubscriptionRenewalJob
{
    private readonly TierraDbContext _context;
    private readonly ILogger<SubscriptionRenewalJob> _logger;

    public SubscriptionRenewalJob(TierraDbContext context, ILogger<SubscriptionRenewalJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    
    /// Scans all active subscriptions across all tenants using IgnoreQueryFilters().
    /// Renews any subscription whose billing cycle has ended and generates a new invoice.

    public async Task ProcessRenewalsAsync()
    {
        _logger.LogInformation("⏳ [Hangfire] Starting automated subscription renewal check at {Time}", DateTime.UtcNow);

        // Crucial: Use .IgnoreQueryFilters() so Hangfire can inspect ALL tenants in the database
        var expiredSubscriptions = await _context.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Tenant)
            .Where(s => s.Status == SubscriptionStatus.Active && s.CurrentPeriodEnd <= DateTime.UtcNow)
            .ToListAsync();

        if (!expiredSubscriptions.Any())
        {
            _logger.LogInformation("✅ [Hangfire] No subscriptions currently due for renewal.");
            return;
        }

        foreach (var sub in expiredSubscriptions)
        {
            var tenantName = sub.Tenant?.Name ?? sub.TenantId.ToString();
            _logger.LogInformation("🔄 [Hangfire] Renewing subscription for Tenant: {Tenant} (Tier: {Tier})", tenantName, sub.Tier);

            // 1. Advance the billing period by 1 month
            sub.CurrentPeriodStart = DateTime.UtcNow;
            sub.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);

            // 2. If it's a paid plan (Pro or Enterprise), generate the recurring renewal invoice
            if (sub.MonthlyPrice > 0)
            {
                var invoice = new Invoice
                {
                    TenantId = sub.TenantId,
                    InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                    Amount = sub.MonthlyPrice,
                    Currency = "ZAR",
                    Status = InvoiceStatus.Paid,
                    CreatedAt = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14)
                };

                _context.Invoices.Add(invoice);
                _logger.LogInformation("📄 [Hangfire] Generated renewal invoice {InvoiceNumber} for R{Amount} ZAR", invoice.InvoiceNumber, invoice.Amount);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("🎉 [Hangfire] Completed renewals for {Count} tenant(s).", expiredSubscriptions.Count);
    }
}