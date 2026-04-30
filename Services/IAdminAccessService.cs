using System.Security.Claims;

namespace DigifyCXIntranet.Services;

public interface IAdminAccessService
{
    bool IsAdmin(ClaimsPrincipal user);
}
