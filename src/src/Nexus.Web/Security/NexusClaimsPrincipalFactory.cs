using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Security;

/// <summary>
/// Extends the default Identity claims factory (which already adds role
/// claims for every role the user belongs to, since Identity is registered
/// with a TRole type) to also attach an "org_id" claim. ICurrentTenant reads
/// this claim to drive NexusDbContext's global query filters, so this class
/// is what actually makes multi-tenancy enforcement (§22) take effect end to
/// end rather than just being defined in the DbContext.
/// </summary>
public class NexusClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public NexusClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("org_id", user.OrganizationId.ToString()));
        return principal;
    }
}
