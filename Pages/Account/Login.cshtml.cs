using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthModeOptions _authModeOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IIdentifierRateLimiter _identifierRateLimiter;

    public LoginModel(
        IOptions<AuthModeOptions> authModeOptions,
        IWebHostEnvironment environment,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IIdentifierRateLimiter identifierRateLimiter)
    {
        _authModeOptions = authModeOptions.Value;
        _environment = environment;
        _userManager = userManager;
        _auditService = auditService;
        _identifierRateLimiter = identifierRateLimiter;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<DevelopmentUserOption> DemoUsers =>
        _authModeOptions.DevelopmentUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.Username))
            .ToList();

    public bool ShowDemoCredentials => _environment.IsDevelopment();
    public string ReturnUrl { get; private set; } = "/Index";
    public bool ShowForgotPasswordPrompt { get; private set; }
    public string FailedUsername { get; private set; } = string.Empty;

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Index" : returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Index" : returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var inputUsername = Input.Username.Trim();
        var inputPassword = Input.Password;
        if (!_identifierRateLimiter.TryAcquire("login", inputUsername))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ModelState.AddModelError(string.Empty, "Too many requests. Please wait and try again.");
            await _auditService.WriteAsync(
                inputUsername,
                "LoginRateLimited",
                "Account",
                "identifier-limit-rejected",
                succeeded: false,
                errorCode: "RateLimited",
                httpContext: HttpContext);
            return Page();
        }
        var devUser = _environment.IsDevelopment()
            ? _authModeOptions.DevelopmentUsers.FirstOrDefault(
                x => string.Equals(x.Username, inputUsername, StringComparison.OrdinalIgnoreCase))
            : null;

        if (devUser is not null)
        {
            if (devUser.Password != inputPassword)
            {
                return await InvalidLoginAsync(inputUsername, "InvalidCredentials");
            }

            var devPrincipal = AppAuthenticationClaims.CreateDevelopmentPrincipal(
                devUser.Username.Trim(),
                string.IsNullOrWhiteSpace(devUser.DisplayName) ? devUser.Username : devUser.DisplayName,
                AppRoles.NormalizeOrEmployee(devUser.Role));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, devPrincipal);
            await _auditService.WriteAsync(
                devUser.Username,
                "LoginSucceeded",
                "Account",
                "source=development-user",
                httpContext: HttpContext);

            return RedirectToLocal(ReturnUrl);
        }

        var dbUser = await _userManager.FindByNameAsync(inputUsername);

        if (dbUser is null ||
            dbUser.IsFirstTimeLogin ||
            string.IsNullOrWhiteSpace(dbUser.PasswordHash) ||
            dbUser.PasswordHash.StartsWith("AQAAAAIAAYagAAAAEOf12Welcome", StringComparison.Ordinal))
        {
            return await InvalidLoginAsync(inputUsername, "InvalidCredentials");
        }

        if (!await _userManager.CheckPasswordAsync(dbUser, inputPassword))
        {
            return await InvalidLoginAsync(inputUsername, "InvalidCredentials");
        }

        if (string.IsNullOrWhiteSpace(dbUser.SecurityStamp))
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(dbUser);
            if (!stampResult.Succeeded)
            {
                return await InvalidLoginAsync(inputUsername, "InvalidAccountState");
            }
        }

        var principal = AppAuthenticationClaims.CreateDatabasePrincipal(dbUser, inputUsername);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        await _auditService.WriteAsync(
            dbUser.UserName ?? inputUsername,
            "LoginSucceeded",
            "Account",
            "source=identity-database",
            entityId: dbUser.Id,
            httpContext: HttpContext);

        return RedirectToLocal(ReturnUrl);
    }

    private async Task<IActionResult> InvalidLoginAsync(string username, string errorCode)
    {
        ShowForgotPasswordPrompt = true;
        FailedUsername = username;
        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        await _auditService.WriteAsync(
            username,
            "LoginFailed",
            "Account",
            "source=cookie-login",
            succeeded: false,
            errorCode: errorCode,
            httpContext: HttpContext);
        return Page();
    }

    private IActionResult RedirectToLocal(string returnUrl)
    {
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage("/Index");
    }

    public class InputModel
    {
        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, StringLength(256)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
