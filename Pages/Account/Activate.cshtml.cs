using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Account;

[AllowAnonymous]
public class ActivateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ActivationOptions _activationOptions;
    private readonly IAuditService _auditService;
    private readonly IIdentifierRateLimiter _identifierRateLimiter;
    private readonly UserManager<ApplicationUser> _userManager;

    public ActivateModel(
        ApplicationDbContext db,
        IOptions<ActivationOptions> activationOptions,
        IAuditService auditService,
        IIdentifierRateLimiter identifierRateLimiter,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _activationOptions = activationOptions.Value;
        _auditService = auditService;
        _identifierRateLimiter = identifierRateLimiter;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool AlreadyActivated { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(120)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Default password is required.")]
        [StringLength(256)]
        [DataType(DataType.Password)]
        [Display(Name = "Default Password")]
        public string DefaultPassword { get; set; } = string.Empty;
    }

    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value
            .Replace("\u00a0", " ")
            .Replace("\u200b", "")
            .Replace("\u200c", "")
            .Replace("\t", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
        return s;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AccountActivationSession.Clear(TempData);
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields correctly.";
            return Page();
        }

        if (!_identifierRateLimiter.TryAcquire("account-activation", Input.FullName))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ErrorMessage = "Too many requests. Please wait and try again.";
            await _auditService.WriteAsync(
                Input.FullName,
                "ActivationRateLimited",
                "Account",
                "identifier-limit-rejected",
                succeeded: false,
                errorCode: "RateLimited",
                httpContext: HttpContext);
            return Page();
        }

        if (!string.Equals(Input.DefaultPassword, _activationOptions.DefaultPassword, StringComparison.Ordinal))
        {
            await AuditFailureAsync("InvalidActivationDetails");
            ErrorMessage = "We could not verify those activation details. Check the information or contact HR or IT support.";
            return Page();
        }

        var normalizedInput = Normalise(Input.FullName);
        var derivedUsername = normalizedInput.Replace(" ", ".");

        // AsNoTracking() is required here because the Zendesk webhook resets
        // IsFirstTimeLogin via raw SQL (ExecuteSqlRawAsync), which bypasses
        // EF's change tracker. Without AsNoTracking(), EF may return a stale
        // in-memory entity where IsFirstTimeLogin is still false, incorrectly
        // blocking re-activation after a password reset ticket is approved.
        var matchingUsers = await _db.Users
            .OfType<ApplicationUser>()
            .AsNoTracking()
            .Where(u =>
                (u.DisplayName != null && u.DisplayName.ToLower() == normalizedInput) ||
                (u.UserName != null && u.UserName.ToLower() == derivedUsername))
            .Take(2)
            .ToListAsync();

        if (matchingUsers.Count != 1)
        {
            await AuditFailureAsync("InvalidActivationDetails");
            ErrorMessage = "We could not verify those activation details. Check the information or contact HR or IT support.";
            return Page();
        }

        var user = matchingUsers[0];

        // ── Already-activated guard ─────────────────────────────────────
        // IsFirstTimeLogin is the single source of truth.
        // The Zendesk webhook resets it to true when an employee raises a
        // forgot-password ticket, which allows them to re-activate here
        // without any other user data (profile, canteen orders, etc.) being
        // affected. If IsFirstTimeLogin is false the employee has already
        // set their own password and the account is considered active.
        if (!user.IsFirstTimeLogin)
        {
            await _auditService.WriteAsync(
                user.UserName ?? Input.FullName,
                "ActivationRejected",
                "Account",
                "reason=already-activated",
                succeeded: false,
                entityId: user.Id,
                errorCode: "AlreadyActivated",
                httpContext: HttpContext);
            AlreadyActivated = true;
            return Page();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Bind the grant to the account's current security stamp. Password resets
        // invalidate this token, even if an old encrypted TempData cookie is replayed.
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        AccountActivationSession.Issue(TempData, user, resetToken, ip, DateTimeOffset.UtcNow);

        await _auditService.WriteAsync(
            user.UserName ?? Input.FullName,
            "ActivationVerified",
            "Account",
            "password-setup-required",
            entityId: user.Id,
            httpContext: HttpContext);

        return RedirectToPage("/Account/ResetPassword");
    }

    private Task AuditFailureAsync(string errorCode) => _auditService.WriteAsync(
        Input.FullName,
        "ActivationRejected",
        "Account",
        "reason=invalid-details",
        succeeded: false,
        errorCode: errorCode,
        httpContext: HttpContext);

}
