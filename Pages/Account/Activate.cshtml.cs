using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public ActivateModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
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
        [Display(Name = "Personal Email")]
        public string PersonalEmail { get; set; } = string.Empty;
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
        // Derive the username format this app uses (e.g. "Tia Chetty" → "tia.chetty")
        var derivedUsername = normalizedInput.Replace(" ", ".");

        // Match by DisplayName OR by username derived from the entered name
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                (u.DisplayName != null && u.DisplayName.ToLower() == normalizedInput) ||
                (u.UserName    != null && u.UserName.ToLower()    == derivedUsername));

        if (user == null)
        {
            ErrorMessage = "No employee record was found matching that name. " +
                           "Please check the spelling (e.g. \"Tia Chetty\") or contact HR.";
            return Page();
        }

        // Personal email already on record — account is already activated
        if (!string.IsNullOrWhiteSpace(user.PersonalEmail))
        {
            AlreadyActivated = true;
            return Page();
        }

        // Save personal email and log activation attempt with IP + timestamp
        user.PersonalEmail = Input.PersonalEmail.Trim().ToLower();
        await _userManager.UpdateAsync(user);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            ip = forwardedFor.ToString().Split(',')[0].Trim();

        _db.AccountActivationLogs.Add(new AccountActivationLog
        {
            UserId       = user.Id,
            DisplayName  = user.DisplayName ?? user.UserName ?? string.Empty,
            PersonalEmail = user.PersonalEmail,
            IpAddress    = ip,
            ActivatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["ActivationUserId"] = user.Id;
        return RedirectToPage("/Account/ResetPassword");
    }
}
