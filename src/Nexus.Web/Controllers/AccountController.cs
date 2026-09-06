using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Controllers;

/// <summary>
/// Minimal but real (not scaffolded/placeholder) account flow. The first
/// person to register against a freshly seeded database becomes a
/// SystemAdministrator for that organization - a standard single-tenant
/// bootstrap pattern - and everyone after that registers as a Viewer, which
/// an administrator can then promote via direct role assignment (a full
/// admin UI for role management is a natural next addition, see README).
/// </summary>
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly NexusDbContext _db;

    public AccountController(
        UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager, NexusDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "CommandCenter");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password, string displayName)
    {
        foreach (var role in NexusRoles.All)
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

        // Attach to the single seeded organization for this reference build.
        // A real multi-tenant onboarding flow would let the user create or
        // join a specific organization instead of defaulting to the first one.
        var org = await _db.Organizations.FirstOrDefaultAsync();
        if (org is null)
        {
            ModelState.AddModelError(string.Empty, "No organization exists yet. Run database seeding first.");
            return View();
        }

        // Registration occurs before authentication, so the tenant claim is not
        // available yet. This is the one deliberate system-level read: scope it
        // explicitly to the selected organization while bypassing the user query
        // filter, rather than treating an unresolved tenant as "first user".
        var isFirstUser = !await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.OrganizationId == org.Id);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            OrganizationId = org.Id
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View();
        }

        await _userManager.AddToRoleAsync(user, isFirstUser ? NexusRoles.SystemAdministrator : NexusRoles.Viewer);
        await _signInManager.SignInAsync(user, isPersistent: true);

        return RedirectToAction("Index", "CommandCenter");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }
}
