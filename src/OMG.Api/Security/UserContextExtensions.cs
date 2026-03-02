using System.Security.Claims;
using OMG.Management.Domain.Gardens;

namespace OMG.Api.Security;

public static class UserContextExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        if (principal.Identity is null || !principal.Identity.IsAuthenticated)
        {
            return null;
        }

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirst(ClaimTypes.Name)
                      ?? principal.FindFirst("sub");

        if (idClaim is null)
        {
            return null;
        }

        return Guid.TryParse(idClaim.Value, out var id) ? id : null;
    }

    public static UserId? GetDomainUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();
        return userId is null ? null : new UserId(userId.Value);
    }
}

