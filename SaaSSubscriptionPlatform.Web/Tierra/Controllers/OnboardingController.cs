using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tierra.Data;
using Tierra.Models;
using Tierra.Services;

namespace Tierra.Controllers;

public class OnboardingController : Controller
{
    private readonly TierraDbContext _context;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public OnboardingController(TierraDbContext context, IPasswordHasher<AppUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // GET: /Onboarding
    [HttpGet]
    public IActionResult Index()
    {
        return View(new OnboardTenantViewModel());
    }

    // POST: /Onboarding
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OnboardTenantViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Ensure slug is unique
        string normalizedSlug = model.Slug.Trim().ToLower();
        bool slugExists = await _context.Tenants.AnyAsync(t => t.Slug == normalizedSlug);
        if (slugExists)
        {
            ModelState.AddModelError("Slug", "This workspace slug is already taken. Please pick another.");
            return View(model);
        }

        // 2. Ensure owner email is unique across the platform
        string normalizedEmail = model.OwnerEmail.Trim().ToLower();
        bool emailExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            ModelState.AddModelError("OwnerEmail", "An account with this email address already exists. Please log in.");
            return View(model);
        }

        // 3. Create the new Tenant (business entity)
        var tenant = new Tenant
        {
            Name = model.CompanyName.Trim(),
            Slug = normalizedSlug,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Tenants.Add(tenant);

        // 4. Provision default Free tier Subscription (R0.00 / month)
        var subscription = new Subscription
        {
            TenantId = tenant.Id,
            Tier = SubscriptionTier.Free,
            Status = SubscriptionStatus.Active,
            MonthlyPrice = 0.00m,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };

        _context.Subscriptions.Add(subscription);

        // 5. Create the Initial Workspace Owner
        var owner = new AppUser
        {
            TenantId = tenant.Id,
            FullName = model.OwnerFullName.Trim(),
            Email = normalizedEmail,
            Role = TenantRole.Owner,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        owner.PasswordHash = _passwordHasher.HashPassword(owner, model.Password);
        _context.Users.Add(owner);

        // 6. Save all records to SQL Server
        await _context.SaveChangesAsync();

        // 7. Automatically sign in the Owner
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()),
            new Claim(ClaimTypes.Name, owner.FullName),
            new Claim(ClaimTypes.Email, owner.Email),
            new Claim(ClaimTypes.Role, owner.Role.ToString()), // "Owner"
            new Claim("TenantId", tenant.Id.ToString()),
            new Claim("TenantName", tenant.Name)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // 8. Set active tenant cookie
        SetTenantCookie(tenant.Id);

        TempData["SuccessMessage"] = $"🎉 Welcome to Tierra! Workspace '{tenant.Name}' created, and you are signed in as Owner.";
        return RedirectToAction("Index", "Home");
    }

    // GET: /Onboarding/Workspaces (Browse and switch workspaces)
    [HttpGet]
    public async Task<IActionResult> Workspaces()
    {
        var tenants = await _context.Tenants
            .Include(t => t.Subscription)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(tenants);
    }

    // POST: /Onboarding/Switch (Switch into a different tenant workspace)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Switch(Guid tenantId)
    {
        SetTenantCookie(tenantId);
        TempData["SuccessMessage"] = "Switched workspace successfully!";
        return RedirectToAction("Index", "Home");
    }

    private void SetTenantCookie(Guid tenantId)
    {
        Response.Cookies.Append(TenantService.TenantCookieKey, tenantId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }
}