using System.ComponentModel.DataAnnotations;
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

namespace DigifyCXIntranet.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public ResetPasswordModel(ApplicationDbContext db, IPasswordHasher<ApplicationUser> hasher)
    {
        _db     = db;
        _hasher = hasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage  { get; set; }
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// The username passed from Activate via TempData — used to identify which
    /// account is being set up without requiring the user to re-enter their name.
    /// </summary>
    public string? ActivationUsername { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        // Keep TempData alive across the GET so the POST can still read it
        ActivationUsername = TempData.Peek("ActivationUsername") as string;

        if (string.IsNullOrWhiteSpace(ActivationUsername))
        {
            // No activation context — redirect back to Activate
            RedirectToPage("/Account/Activate");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Read the username set by Activate.cshtml.cs
        var username = TempData["ActivationUsername"] as string;
        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorMessage = "Your activation session has expired. Please start the activation process again.";
            return Page();
        }

        // Look up the user by their system username (set by the sync worker)
        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.UserName != null &&
                                      u.UserName.ToLower() == username.ToLower());

        if (user == null)
        {
            ErrorMessage = "Account not found. Please contact IT support.";
            return Page();
        }

        // Hash and save the new password
        user.PasswordHash    = _hasher.HashPassword(user, Input.Password);
        user.IsFirstTimeLogin = false;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        // Auto sign-in the user after setting their password
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,      user.UserName ?? user.DisplayName ?? username),
            new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(user.DisplayName)
                ? (user.UserName ?? username)
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
