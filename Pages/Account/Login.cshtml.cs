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
    private readonly IAuditService _auditService;

    public LoginModel(
        IOptions<AuthModeOptions> authModeOptions,
        IWebHostEnvironment environment,
        ApplicationDbContext db,
        IPasswordHasher<ApplicationUser> hasher,
        IAuditService auditService)
    {
        _authModeOptions = authModeOptions.Value;
        _environment = environment;
        _db = db;
        _hasher = hasher;
        _auditService = auditService;
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
            return Page();

        var inputUsername = Input.Username.Trim();
        var inputPassword = Input.Password;

        // ── 1. Check hardcoded dev credentials first (admin, moham, finance, etc.) ──
        var devUser = _authModeOptions.DevelopmentUsers.FirstOrDefault(
            x => string.Equals(x.Username, inputUsername, StringComparison.OrdinalIgnoreCase));

        if (devUser is not null)
        {
            if (devUser.Password != inputPassword)
            {
                await _auditService.WriteAsync(inputUsername, "LoginFailed", "Account", "source=development-user", false, errorCode: "InvalidCredentials", httpContext: HttpContext);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return Page();
            }

            // Dev user matched — sign in via cookie claim (unchanged from original)
            var devClaims = new List<Claim>
            {
                new(ClaimTypes.Name,      devUser.Username.Trim()),
                new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(devUser.DisplayName)
                    ? devUser.Username
                    : devUser.DisplayName),
                new(ClaimTypes.Role,      string.IsNullOrWhiteSpace(devUser.Role)
                    ? AppRoles.Agent
                    : devUser.Role.Trim())
            };

            var devIdentity  = new ClaimsIdentity(devClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var devPrincipal = new ClaimsPrincipal(devIdentity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, devPrincipal);
            await _auditService.WriteAsync(devUser.Username, "LoginSucceeded", "Account", "source=development-user", httpContext: HttpContext);

            return Url.IsLocalUrl(ReturnUrl)
                ? LocalRedirect(ReturnUrl)
                : RedirectToPage("/Index");
        }

        // ── 2. Fall through to database for real employee accounts ──
        var dbUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.UserName != null &&
                u.UserName.ToLower() == inputUsername.ToLower());

        if (dbUser == null)
        {
            await _auditService.WriteAsync(inputUsername, "LoginFailed", "Account", "source=database-user", false, errorCode: "InvalidCredentials", httpContext: HttpContext);
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        // Employee must have completed account activation (PersonalEmail set + PasswordHash set)
        if (string.IsNullOrWhiteSpace(dbUser.PersonalEmail))
        {
            await _auditService.WriteAsync(inputUsername, "LoginFailed", "Account", "reason=not-activated", false, dbUser.Id, "NotActivated", HttpContext);
            ModelState.AddModelError(string.Empty,
                "Your account has not been activated yet. Please visit the Activate Account page first.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(dbUser.PasswordHash) ||
            dbUser.PasswordHash.StartsWith("AQAAAAIAAYagAAAAEOf12Welcome"))
        {
            await _auditService.WriteAsync(inputUsername, "LoginFailed", "Account", "reason=password-not-set", false, dbUser.Id, "PasswordNotSet", HttpContext);
            ModelState.AddModelError(string.Empty,
                "You have not set a password yet. Please complete account activation first.");
            return Page();
        }

        // Verify the password hash
        var verificationResult = _hasher.VerifyHashedPassword(dbUser, dbUser.PasswordHash, inputPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            await _auditService.WriteAsync(inputUsername, "LoginFailed", "Account", "source=database-user", false, dbUser.Id, "InvalidCredentials", HttpContext);
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        // Sign the employee in via cookie auth — same structure as dev users above
        var role = string.IsNullOrWhiteSpace(dbUser.CustomRole) ? AppRoles.Agent : dbUser.CustomRole;
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
        await _auditService.WriteAsync(dbUser.UserName ?? inputUsername, "LoginSucceeded", "Account", "source=database-user", true, dbUser.Id, httpContext: HttpContext);

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
