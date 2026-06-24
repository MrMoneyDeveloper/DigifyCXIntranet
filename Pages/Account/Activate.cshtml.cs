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
    private readonly UserManager<ApplicationUser> _userManager;
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
        _userManager = userManager;
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

    // Normalise: lowercase, collapse whitespace, trim
    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value
            .Replace("\u00a0", " ")
            .Replace("\u200b", "")
            .Replace("\u200c", "")
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

        // ── 1. Validate default password ─────────────────────────────────
        if (!string.Equals(Input.DefaultPassword, _activationOptions.DefaultPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "The default password you entered is incorrect. Please check with your manager or HR.";
            return Page();
        }

        var normalizedInput = Normalise(Input.FullName);
        var derivedUsername = normalizedInput.Replace(" ", ".");

        // ── 2. Validate name exists in Google Sheet (Apps Script) ─────────
        var nameExistsInSheet = await CheckNameInSheetAsync(Input.FullName.Trim());
        if (!nameExistsInSheet)
        {
            ErrorMessage = "Your name was not found in the active employee list. Please contact HR if you believe this is an error.";
            return Page();
        }

        // ── 3. Find the user in the local DB ──────────────────────────────
        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u =>
                (u.DisplayName != null && u.DisplayName.ToLower() == normalizedInput) ||
                (u.UserName != null && u.UserName.ToLower() == derivedUsername));

        if (user == null)
        {
            ErrorMessage = "No employee record was found matching that name. Please check the spelling or contact HR.";
            return Page();
        }

        // ── 4. Check if already activated (password already set) ──────────
        if (!string.IsNullOrWhiteSpace(user.PasswordHash) &&
            !user.PasswordHash.StartsWith("AQAAAAIAAYagAAAAEOf12Welcome"))
        {
            AlreadyActivated = true;
            return Page();
        }

        // ── 5. Record IP + timestamp, then redirect to Set Password ───────
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            ip = forwardedFor.ToString().Split(',')[0].Trim();

        _db.AccountActivationLogs.Add(new AccountActivationLog
        {
            UserId       = user.Id,
            DisplayName  = user.DisplayName ?? user.UserName ?? string.Empty,
            PersonalEmail = string.Empty,   // no longer collected at activation step
            IpAddress    = ip,
            ActivatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Pass the username to the Set Password page via TempData
        TempData["ActivationUsername"] = user.UserName;
        return RedirectToPage("/Account/ResetPassword");
    }

    // ── Calls the Apps Script endpoint and checks whether fullName is present ──
    private async Task<bool> CheckNameInSheetAsync(string fullName)
    {
        if (string.IsNullOrWhiteSpace(_activationOptions.SheetApiUrl))
        {
            _logger.LogWarning("[Activation] SheetApiUrl is not configured — skipping Google Sheet validation.");
            return true;   // fail-open so activation is not blocked if URL is missing
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var cts    = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Clamp(_activationOptions.SheetTimeoutSeconds, 5, 60)));

            var response = await client.GetAsync(_activationOptions.SheetApiUrl, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Activation] Sheet API returned {Status} — failing open.", response.StatusCode);
                return true;
            }

            var content  = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return true;   // unexpected format — fail open

            var normalizedTarget = Normalise(fullName);

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                // The Apps Script returns objects with a "fullName" property
                if (element.TryGetProperty("fullName", out var nameProp))
                {
                    if (string.Equals(Normalise(nameProp.GetString()), normalizedTarget, StringComparison.Ordinal))
                        return true;
                }
                // Fallback: element might be a plain string
                else if (element.ValueKind == JsonValueKind.String)
                {
                    if (string.Equals(Normalise(element.GetString()), normalizedTarget, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Activation] Failed to reach Google Sheet API — failing open.");
            return true;   // fail-open: don't block activation on a network hiccup
        }
    }
}
