using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DigifyCXIntranet.Pages.Account;

[AllowAnonymous]
public class ActivateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public ActivateModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? DebugInfo { get; set; }   // only shown in dev; remove when done
    public bool AlreadyActivated { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Personal email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Personal Email Address")]
        public string PersonalEmail { get; set; } = string.Empty;
    }

    // Normalise: lowercase, collapse all whitespace variants to single space, trim
    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value
            .Replace("\u00a0", " ")   // non-breaking space
            .Replace("\u200b", "")    // zero-width space
            .Replace("\u200c", "")    // zero-width non-joiner
            .Replace("\t", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim().ToLower();
        return s;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields correctly.";
            return Page();
        }

        var normalizedInput = Input.FullName.Trim().ToLower();
        var derivedUsername = normalizedInput.Replace(" ", ".");

        // Match by DisplayName OR by username derived from the name (firstname.lastname)
        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u =>
                (u.DisplayName != null && u.DisplayName.ToLower() == normalizedInput) ||
                (u.UserName != null && u.UserName.ToLower() == derivedUsername));

        if (user == null)
        {
            await _auditService.WriteAsync(Input.FullName, "AccountActivationFailed", "Account", "reason=no-match", false, errorCode: "NoEmployeeMatch", httpContext: HttpContext);
            ErrorMessage = "No employee record was found matching that name. Please check the spelling or contact HR.";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(user.PersonalEmail))
        {
            await _auditService.WriteAsync(user.UserName ?? normalizedInput, "AccountActivationSkipped", "Account", "reason=already-activated", true, user.Id, httpContext: HttpContext);
            AlreadyActivated = true;
            return Page();
        }

        user.PersonalEmail = Input.PersonalEmail.Trim().ToLower();
        await _userManager.UpdateAsync(user);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            ip = forwardedFor.ToString().Split(',')[0].Trim();

        _db.AccountActivationLogs.Add(new AccountActivationLog
        {
            UserId = user.Id,
            DisplayName = user.DisplayName ?? user.UserName ?? string.Empty,
            PersonalEmail = user.PersonalEmail,
            IpAddress = ip,
            ActivatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(user.UserName ?? normalizedInput, "AccountActivated", "Account", "personalEmailUpdated=true", true, user.Id, httpContext: HttpContext);

        TempData["ActivationUsername"] = user.UserName;
        return RedirectToPage("/Account/ResetPassword");
    }
}
