using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthModeOptions _authModeOptions;
    private readonly IWebHostEnvironment _environment;

    public LoginModel(IOptions<AuthModeOptions> authModeOptions, IWebHostEnvironment environment)
    {
        _authModeOptions = authModeOptions.Value;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<DevelopmentUserOption> DemoUsers =>
        _authModeOptions.DevelopmentUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.Username))
            .ToList();
    public bool ShowDemoCredentials => _environment.IsDevelopment();

    public string ReturnUrl { get; private set; } = "/Index";

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

        var user = _authModeOptions.DevelopmentUsers.FirstOrDefault(
            x => string.Equals(x.Username, Input.Username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null || user.Password != Input.Password)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username.Trim()),
            new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? AppRoles.Agent : user.Role.Trim())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }

    public class InputModel
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
