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

public class AccountController : Controller
{
    private readonly TierraDbContext _context;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public AccountController(TierraDbContext context, IPasswordHasher<AppUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string normalizedEmail = model.Email.Trim().ToLower();

        // 1. Find the user across all tenants using IgnoreQueryFilters()
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // 2. Cryptographically verify the password hash
        var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // 3. Construct the User Claims with TenantId and Role
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()), // "Owner", "Admin", or "Member"
            new Claim("TenantId", user.TenantId.ToString()),
            new Claim("TenantName", user.Tenant?.Name ?? "Workspace")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        // 4. Issue the encrypted authentication cookie
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        // 5. Also set the tenant cookie for backward compatibility
        Response.Cookies.Append(TenantService.TenantCookieKey, user.TenantId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        TempData["SuccessMessage"] = $"Welcome back, {user.FullName}! Logged into {user.Tenant?.Name}.";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(TenantService.TenantCookieKey);
        TempData["SuccessMessage"] = "You have successfully logged out.";
        return RedirectToAction("Login");
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // POST: /Account/DemoLogin (Quick 1-click role testing for portfolios & interviews)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoLogin(string role)
    {
        // 1. Ensure at least one Tenant exists
        var tenant = await _context.Tenants
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync();

        if (tenant == null)
        {
            tenant = new Tenant
            {
                Name = "Apex Enterprises",
                Slug = "apex",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Tenants.Add(tenant);
            _context.Subscriptions.Add(new Subscription
            {
                TenantId = tenant.Id,
                Tier = SubscriptionTier.Pro,
                MonthlyPrice = 499.00m,
                Status = SubscriptionStatus.Active,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
            });
            await _context.SaveChangesAsync();
        }

        // 2. Parse target role
        if (!Enum.TryParse<TenantRole>(role, true, out var targetRole))
        {
            targetRole = TenantRole.Owner;
        }

        // 3. Find or create demo user
        string demoEmail = $"{targetRole.ToString().ToLower()}@apex.com";
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == demoEmail);

        if (user == null)
        {
            user = new AppUser
            {
                TenantId = tenant.Id,
                FullName = targetRole switch
                {
                    TenantRole.ITManager => "Alex Rivera (IT Lead)",
                    TenantRole.Owner => "Marcus Sterling (CEO)",
                    TenantRole.Admin => "Elena Rostova (Operations)",
                    _ => "David Kweku (Product Team)"
                },
                Email = demoEmail,
                Role = targetRole,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, "Demo123!");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // 4. Issue Claims & Sign In
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("TenantId", user.TenantId.ToString()),
            new Claim("TenantName", tenant.Name)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        Response.Cookies.Append(TenantService.TenantCookieKey, user.TenantId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        TempData["SuccessMessage"] = $"Logged in as Demo {targetRole}: {user.FullName}!";
        return RedirectToAction("Index", "Home");
    }
}