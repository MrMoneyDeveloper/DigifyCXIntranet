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
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;

    public ResetPasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
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
        // Keep TempData alive across the GET so the page can optionally pre-fill
        TempData.Keep("ActivationUsername");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Find user by PersonalEmail recorded during activation
        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u =>
                u.PersonalEmail != null &&
                u.PersonalEmail.ToLower() == Input.Email.Trim().ToLower());

        if (user == null)
        {
            ErrorMessage = "No account was found with that personal email. Please go back and activate your account first.";
            return Page();
        }

        // Generate a reset token and apply the new password
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, Input.Password);

        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        // Mark account as fully activated (no longer first-time)
        user.IsFirstTimeLogin = false;
        await _userManager.UpdateAsync(user);

        // Automatically sign the user in
        await _signInManager.SignInAsync(user, isPersistent: false);

        SuccessMessage = "Password set successfully! Signing you in…";
        return RedirectToPage("/Index");
    }
}
