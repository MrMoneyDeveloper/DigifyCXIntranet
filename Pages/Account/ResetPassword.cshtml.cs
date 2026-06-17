using System.Security.Claims;
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
using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public ResetPasswordModel(ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Personal email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Personal Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        TempData.Keep("ActivationUserId");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Query ApplicationUser directly — no .OfType<> needed
        var normalised = Input.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.PersonalEmail != null &&
                u.PersonalEmail.ToLower() == normalised);

        if (user == null)
        {
            ErrorMessage = "No account was found with that personal email. Please go back and activate your account first.";
            return Page();
        }

        // Hash and save the new password directly — no UserManager needed
        user.PasswordHash = _hasher.HashPassword(user, Input.Password);
        user.IsFirstTimeLogin = false;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        // Sign the user in via cookie auth — same mechanism as Login.cshtml.cs
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,      user.UserName ?? user.DisplayName),
            new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(user.DisplayName)
                ? (user.UserName ?? string.Empty)
                : user.DisplayName),
            new(ClaimTypes.Role,      string.IsNullOrWhiteSpace(user.CustomRole)
                ? AppRoles.Agent
                : user.CustomRole)
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}
