using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Audit;

namespace Nexus.Web.Controllers;

/// <summary>
/// Minimal, real role-management screen (§23): lists users in the current
/// organization and lets a System Administrator assign/revoke roles. Every
/// change is audited. This replaces "roles are only ever assigned at
/// registration" with an actual administrative workflow.
/// </summary>
[Authorize(Roles = NexusRoles.SystemAdministrator)]
public class AdminController : Controller
{
    private readonly NexusDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public AdminController(NexusDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var users = await _db.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var rows = new List<AdminUserRow>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            rows.Add(new AdminUserRow { User = u, Roles = roles.ToList() });
        }

        ViewBag.AllRoles = NexusRoles.All;
        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(string userId, string role, CancellationToken ct)
    {
        if (!NexusRoles.All.Contains(role)) return BadRequest("Unknown role.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (!await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.AddToRoleAsync(user, role);
            await _audit.RecordAsync(
                user.OrganizationId, User?.Identity?.Name, "User.RoleAssigned", nameof(ApplicationUser), Guid.Parse(user.Id),
                afterState: role, ct: ct);
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeRole(string userId, string role, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.RemoveFromRoleAsync(user, role);
            await _audit.RecordAsync(
                user.OrganizationId, User?.Identity?.Name, "User.RoleRevoked", nameof(ApplicationUser), Guid.Parse(user.Id),
                beforeState: role, ct: ct);
        }

        return RedirectToAction(nameof(Users));
    }
}

public class AdminUserRow
{
    public ApplicationUser User { get; set; } = default!;
    public List<string> Roles { get; set; } = new();
}
