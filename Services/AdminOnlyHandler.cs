using Microsoft.AspNetCore.Authorization;

namespace DigifyCXIntranet.Services;

public class AdminOnlyHandler : AuthorizationHandler<AdminOnlyRequirement>
{
    private readonly IAdminAccessService _adminAccessService;

    public AdminOnlyHandler(IAdminAccessService adminAccessService)
    {
        _adminAccessService = adminAccessService;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOnlyRequirement requirement)
    {
        if (_adminAccessService.IsAdmin(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
