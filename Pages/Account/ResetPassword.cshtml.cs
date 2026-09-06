using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<ResetPasswordModel> _logger;
    private readonly IIdentifierRateLimiter _identifierRateLimiter;

    public ResetPasswordModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ILogger<ResetPasswordModel> logger,
        IIdentifierRateLimiter identifierRateLimiter)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
        _identifierRateLimiter = identifierRateLimiter;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? ActivationUsername { get; private set; }

    public IActionResult OnGet()
    {
        var session = AccountActivationSession.Read(TempData, DateTimeOffset.UtcNow);
        if (session is null)
        {
            AccountActivationSession.Clear(TempData);
            return RedirectToPage("/Account/Activate");
        }

        ActivationUsername = session.Username;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var session = AccountActivationSession.Read(TempData, DateTimeOffset.UtcNow);
        if (session is null)
        {
            AccountActivationSession.Clear(TempData);
            ErrorMessage = "Your activation session has expired. Please start the activation process again.";
            return Page();
        }

        var username = session.Username;
        ActivationUsername = username;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!_identifierRateLimiter.TryAcquire("password-reset", username))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ErrorMessage = "Too many requests. Please wait and try again.";
            return Page();
        }

        var user = await _userManager.FindByIdAsync(session.UserId);
        if (!session.Matches(user))
        {
            AccountActivationSession.Clear(TempData);
            await _auditService.WriteAsync(
                username,
                "PasswordActivationFailed",
                "Account",
                "reason=invalid-activation-state",
                succeeded: false,
                errorCode: "InvalidActivationState",
                httpContext: HttpContext);
            ErrorMessage = "The activation request is no longer valid. Please start again or contact IT support.";
            return Page();
        }

        // Persist the password and activation flag together through Identity's
        // concurrency check, so a failed second save cannot leave partial activation.
        user!.IsFirstTimeLogin = false;
        var resetResult = await _userManager.ResetPasswordAsync(user, session.ResetToken, Input.Password);
        if (!resetResult.Succeeded)
        {
            user.IsFirstTimeLogin = true;
            // Identity may have changed tracked credentials before a later
            // validation/concurrency failure. Do not let the audit save persist them.
            _db.Entry(user).State = EntityState.Detached;
            var invalidToken = resetResult.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken));
            if (invalidToken)
            {
                AccountActivationSession.Clear(TempData);
                ErrorMessage = "The activation request is no longer valid. Please start again or contact IT support.";
            }

            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(nameof(Input.Password), error.Description);
            }

            await _auditService.WriteAsync(
                username,
                "PasswordActivationFailed",
                "Account",
                invalidToken ? "reason=invalid-activation-token" : "reason=password-validation",
                succeeded: false,
                entityId: user.Id,
                errorCode: invalidToken ? "InvalidActivationToken" : "PasswordValidationFailed",
                httpContext: HttpContext);
            return Page();
        }

        try
        {
            _db.AccountActivationLogs.Add(new AccountActivationLog
            {
                UserId = user.Id,
                DisplayName = user.DisplayName ?? user.UserName ?? string.Empty,
                PersonalEmail = string.Empty,
                IpAddress = session.IpAddress,
                ActivatedUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger.LogError(ex, "Activation log write failed for user {UserId}.", user.Id);
        }

        await _auditService.WriteAsync(
            username,
            "PasswordActivationSucceeded",
            "Account",
            "source=account-activation",
            entityId: user.Id,
            httpContext: HttpContext);

        var principal = AppAuthenticationClaims.CreateDatabasePrincipal(user, username);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        AccountActivationSession.Clear(TempData);

        return RedirectToPage("/Index");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Password is required.")]
        [StringLength(256, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 256 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
