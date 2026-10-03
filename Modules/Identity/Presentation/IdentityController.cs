using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Presentation;

public sealed class IdentityController : Controller
{
    private readonly SignInManager<NexusUser> _signInManager;
    private readonly UserManager<NexusUser> _userManager;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;

    public IdentityController(
        SignInManager<NexusUser> signInManager,
        UserManager<NexusUser> userManager,
        IIdentityService identityService,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _identityService = identityService;
        _auditService = auditService;
    }

    [HttpGet("/Identity/Login")]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        var users = await _identityService.GetAllUsersAsync(cancellationToken);
        return View("~/Views/Identity/Login.cshtml", users);
    }

    [HttpPost("/Identity/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            var res = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
            if (res.Succeeded)
            {
                await _auditService.RecordAsync(user.FullName, user.PrimaryRoleTitle, "Identity", "USER_LOGIN", "NexusUser", user.Email ?? "", $"Authenticated operator {user.Email} ({user.PrimaryRoleTitle}).");
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "CommandCenter");
            }
        }

        TempData["AuthError"] = "Invalid credentials or account locked.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost("/Identity/QuickRoleLogin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickRoleLogin(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await _signInManager.SignInAsync(user, isPersistent: true);
            await _auditService.RecordAsync(user.FullName, user.PrimaryRoleTitle, "Identity", "ROLE_SESSION_SWITCH", "NexusUser", user.Email ?? "", $"Switched operator console session to {user.FullName} [{user.PrimaryRoleTitle}].");
        }
        return RedirectToAction("Index", "CommandCenter");
    }

    [HttpPost("/Identity/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
