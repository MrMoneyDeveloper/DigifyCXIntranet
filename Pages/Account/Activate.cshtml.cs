using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ActivationOptions _activationOptions;
    private readonly ILogger<ActivateModel> _logger;

    public ActivateModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IHttpClientFactory httpClientFactory,
        IOptions<ActivationOptions> activationOptions,
        ILogger<ActivateModel> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _activationOptions = activationOptions.Value;
        _logger = logger;
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

        [Required(ErrorMessage = "Default password is required.")]
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
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields correctly.";
            return Page();
        }

        if (!string.Equals(Input.DefaultPassword, _activationOptions.DefaultPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "The default password you entered is incorrect. Please check with your manager, HR, or a System Admin.";
            return Page();
        }

        var normalizedInput = Normalise(Input.FullName);
        var derivedUsername = normalizedInput.Replace(" ", ".");

        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u =>
                (u.DisplayName != null && u.DisplayName.ToLower() == normalizedInput) ||
                (u.UserName != null && u.UserName.ToLower() == derivedUsername));

        if (user == null)
        {
            var nameExistsInSheet = await CheckNameInSheetAsync(Input.FullName.Trim());
            if (!nameExistsInSheet)
            {
                ErrorMessage = "Your name was not found in the employee registry or the system-admin user list. Please contact HR or IT support.";
                return Page();
            }

            ErrorMessage = "No employee record was found matching that name. Please check the spelling or contact HR.";
            return Page();
        }

        var hasRealPassword = !string.IsNullOrWhiteSpace(user.PasswordHash) &&
                              !user.PasswordHash.StartsWith("AQAAAAIAAYagAAAAEOf12Welcome");

        if (hasRealPassword && !user.IsFirstTimeLogin)
        {
            AlreadyActivated = true;
            return Page();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',')[0].Trim();
        }

        TempData["ActivationUsername"] = user.UserName;
        TempData["ActivationIp"] = ip;
        TempData["ActivationDisplay"] = user.DisplayName ?? user.UserName ?? string.Empty;
        TempData["ActivationUserId"] = user.Id;

        return RedirectToPage("/Account/ResetPassword");
    }

    private async Task<bool> CheckNameInSheetAsync(string fullName)
    {
        if (string.IsNullOrWhiteSpace(_activationOptions.SheetApiUrl))
        {
            _logger.LogWarning("[Activation] SheetApiUrl is not configured; skipping Google Sheet validation.");
            return true;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Clamp(_activationOptions.SheetTimeoutSeconds, 5, 60)));

            var response = await client.GetAsync(_activationOptions.SheetApiUrl, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Activation] Sheet API returned {Status}; failing open.", response.StatusCode);
                return true;
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            var normalizedTarget = Normalise(fullName);

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("fullName", out var nameProp))
                {
                    if (string.Equals(Normalise(nameProp.GetString()), normalizedTarget, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                else if (element.ValueKind == JsonValueKind.String &&
                         string.Equals(Normalise(element.GetString()), normalizedTarget, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Activation] Failed to reach Google Sheet API; failing open.");
            return true;
        }
    }
}
