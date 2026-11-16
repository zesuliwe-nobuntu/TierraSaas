using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tierra.Data;
using Tierra.Models;
using Tierra.Services;

namespace Tierra.Controllers;

[Authorize]
public class BillingController : Controller
{
    private readonly TierraDbContext _context;
    private readonly ITenantService _tenantService;

    public BillingController(TierraDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    // GET: /Billing
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenantId = _tenantService.GetTenantId();
        if (!tenantId.HasValue)
        {
            TempData["SuccessMessage"] = "Please select or create a workspace first.";
            return RedirectToAction("Workspaces", "Onboarding");
        }

        // Global Query Filter automatically filters by the active TenantId
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value);

        var invoices = await _context.Invoices
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        ViewBag.Invoices = invoices;
        return View(subscription);
    }

    // POST: /Billing/ChangePlan
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(SubscriptionTier targetTier)
    {
        var tenantId = _tenantService.GetTenantId();
        if (!tenantId.HasValue)
        {
            return RedirectToAction("Workspaces", "Onboarding");
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value);

        if (subscription == null)
        {
            return NotFound();
        }

        if (subscription.Tier == targetTier)
        {
            TempData["SuccessMessage"] = $"Your workspace is already on the {targetTier} plan.";
            return RedirectToAction("Index");
        }

        // South African Rand pricing tiers
        decimal newPrice = targetTier switch
        {
            SubscriptionTier.Free => 0.00m,
            SubscriptionTier.Pro => 499.00m,
            SubscriptionTier.Enterprise => 1999.00m,
            _ => 0.00m
        };

        subscription.Tier = targetTier;
        subscription.MonthlyPrice = newPrice;
        subscription.CurrentPeriodStart = DateTime.UtcNow;
        subscription.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        subscription.Status = SubscriptionStatus.Active;

        // Simulate payment gateway webhook: generate an invoice for paid upgrades
        if (newPrice > 0)
        {
            var invoice = new Invoice
            {
                TenantId = tenantId.Value,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                Amount = newPrice,
                Currency = "ZAR",
                Status = InvoiceStatus.Paid,
                CreatedAt = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14)
            };

            _context.Invoices.Add(invoice);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"🎉 Plan successfully updated to {targetTier} (R{newPrice:N2}/mo)!";
        return RedirectToAction("Index");
    }
}