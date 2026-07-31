using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthModeOptions _authModeOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public LoginModel(
        IOptions<AuthModeOptions> authModeOptions,
        IWebHostEnvironment environment,
        ApplicationDbContext db,
        IPasswordHasher<ApplicationUser> hasher)
    {
        _authModeOptions = authModeOptions.Value;
        _environment     = environment;
        _db              = db;
        _hasher          = hasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<DevelopmentUserOption> DemoUsers =>
        _authModeOptions.DevelopmentUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.Username))
            .ToList();

    public bool ShowDemoCredentials => _environment.IsDevelopment();

    public string ReturnUrl { get; private set; } = "/Index";

    /// <summary>
    /// Set to true when the user's credentials were rejected due to a wrong password
    /// so the view can show the prominent Forgot Password call-to-action.
    /// </summary>
    public bool ShowForgotPasswordPrompt { get; private set; }

    /// <summary>
    /// The display name that was looked up, carried into the view so the amber banner
    /// can build a pre-filled link to /Account/ForgotPassword.
    /// Using DisplayName (e.g. "Wendy Moodley") instead of the raw username
    /// (e.g. "wendy.moodley") ensures the Zendesk webhook can match the
    /// employee record in the database.
    /// Only populated when ShowForgotPasswordPrompt is true.
    /// </summary>
    public string FailedUsername { get; private set; } = string.Empty;

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Index" : returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Index" : returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var inputUsername = Input.Username.Trim();
        var inputPassword = Input.Password;

        // ── 1. Hardcoded dev credentials ──────────────────────────────────
        var devUser = _environment.IsDevelopment()
            ? _authModeOptions.DevelopmentUsers.FirstOrDefault(
                x => string.Equals(x.Username, inputUsername, StringComparison.OrdinalIgnoreCase))
            : null;

        if (devUser is not null)
        {
            if (devUser.Password != inputPassword)
            {
                // Wrong password for a dev user — show the generic error (no forgot-password
                // prompt for hardcoded dev accounts, they don't use Zendesk).
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return Page();
            }

            var devClaims = new List<Claim>
            {
                new(ClaimTypes.Name,      devUser.Username.Trim()),
                new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(devUser.DisplayName)
                    ? devUser.Username
                    : devUser.DisplayName),
                new(ClaimTypes.Role, AppRoles.NormalizeOrEmployee(devUser.Role))
            };

            var devIdentity  = new ClaimsIdentity(devClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var devPrincipal = new ClaimsPrincipal(devIdentity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, devPrincipal);

            return Url.IsLocalUrl(ReturnUrl)
                ? LocalRedirect(ReturnUrl)
                : RedirectToPage("/Index");
        }

        // ── 2. Database employee accounts ─────────────────────────────────
        var normalizedUsername = inputUsername.ToUpperInvariant();
        var dbUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUsername);

        if (dbUser == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(dbUser.PasswordHash) ||
            dbUser.PasswordHash.StartsWith("AQAAAAIAAYagAAAAEOf12Welcome"))
        {
            ModelState.AddModelError(string.Empty,
                "You have not set a password yet. Please complete account activation first.");
            return Page();
        }

        var verificationResult = _hasher.VerifyHashedPassword(dbUser, dbUser.PasswordHash, inputPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            // Wrong password: flag the view to show the Forgot Password prompt.
            // Pass DisplayName (e.g. "Wendy Moodley") — NOT the raw username
            // (e.g. "wendy.moodley") — so the ForgotPassword page pre-fills
            // the full name that the Zendesk webhook needs to match the DB record.
            ShowForgotPasswordPrompt = true;
            FailedUsername = !string.IsNullOrWhiteSpace(dbUser.DisplayName)
                ? dbUser.DisplayName.Trim()
                : inputUsername;
            ModelState.AddModelError(string.Empty, "Incorrect password.");
            return Page();
        }

        var role        = AppRoles.NormalizeOrEmployee(dbUser.CustomRole);
        var displayName = string.IsNullOrWhiteSpace(dbUser.DisplayName)
            ? (dbUser.UserName ?? inputUsername)
            : dbUser.DisplayName;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,      dbUser.UserName ?? inputUsername),
            new(ClaimTypes.GivenName, displayName),
            new(ClaimTypes.Role,      role)
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Index");
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
