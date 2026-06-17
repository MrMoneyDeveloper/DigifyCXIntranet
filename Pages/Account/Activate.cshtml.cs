using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Pages.Account;

[AllowAnonymous]
public class ActivateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public ActivateModel(ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
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

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Find user by DisplayName (case-insensitive)
        var normalised = Input.FullName.Trim().ToLowerInvariant();
        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.DisplayName != null &&
                u.DisplayName.ToLower() == normalised);

        if (user == null)
        {
            ErrorMessage = "No employee record was found matching that name. Please check the spelling or contact HR.";
            return Page();
        }

        // Personal email already set — account already activated
        if (!string.IsNullOrWhiteSpace(user.PersonalEmail))
        {
            AlreadyActivated = true;
            return Page();
        }

        // Email is empty — record it and proceed to password setup
        user.PersonalEmail = Input.PersonalEmail.Trim().ToLowerInvariant();
        _db.Users.Update(user);

        // Capture IP (respects reverse-proxy X-Forwarded-For header)
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            ip = forwardedFor.ToString().Split(',')[0].Trim();

        // Write security audit log
        _db.AccountActivationLogs.Add(new AccountActivationLog
        {
            UserId      = user.Id,
            DisplayName = user.DisplayName,
            PersonalEmail = user.PersonalEmail,
            IpAddress   = ip,
            ActivatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Pass the user's ID securely to ResetPassword via TempData
        TempData["ActivationUserId"] = user.Id;
        return RedirectToPage("/Account/ResetPassword");
    }
}
