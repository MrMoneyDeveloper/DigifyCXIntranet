using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigifyCXIntranet.Services;

namespace DigifyCXIntranet.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly IAuditService _auditService;

    public LogoutModel(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Logout", "Account", "user-initiated", httpContext: HttpContext);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Account/Login");
    }
}
