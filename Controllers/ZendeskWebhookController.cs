using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Controllers;

/// <summary>
/// Receives a webhook POST from Zendesk when IT approves a password-reset ticket.
/// The endpoint resets the employee back to the first-time-login state so they
/// can go through /Account/Activate exactly as a new employee would.
/// 
/// Security: every request must include the header
///   X-Zendesk-Webhook-Secret: <value from ZendeskWebhook:Secret in config>
/// Zendesk sends this automatically when you configure the webhook.
/// </summary>
[ApiController]
[Route("api/zendesk")]
public class ZendeskWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly ZendeskWebhookOptions _options;
    private readonly ILogger<ZendeskWebhookController> _logger;

    // The placeholder hash that marks an account as "not yet activated".
    // Must match the value used during initial user sync.
    private const string PlaceholderHash =
        "AQAAAAIAAYagAAAAEOf12WelcomeDigifyCX2024!Placeholder";

    public ZendeskWebhookController(
        ApplicationDbContext db,
        IPasswordHasher<ApplicationUser> hasher,
        IOptions<ZendeskWebhookOptions> options,
        ILogger<ZendeskWebhookController> logger)
    {
        _db      = db;
        _hasher  = hasher;
        _options = options.Value;
        _logger  = logger;
    }

    // ── POST api/zendesk/reset-password ──────────────────────────────────
    // Zendesk sends:
    //   Header : X-Zendesk-Webhook-Secret: <secret>
    //   Body   : { "display_name": "Wendy Moodley", "ticket_id": "12345" }
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        // ── 1. Validate the shared secret ────────────────────────────────
        if (!Request.Headers.TryGetValue("X-Zendesk-Webhook-Secret", out var incomingSecret) ||
            !string.Equals(incomingSecret, _options.Secret, StringComparison.Ordinal))
        {
            _logger.LogWarning("[ZendeskWebhook] Rejected request — invalid or missing secret.");
            return Unauthorized(new { error = "Invalid webhook secret." });
        }

        // ── 2. Validate payload ──────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            _logger.LogWarning("[ZendeskWebhook] Rejected request — display_name is empty.");
            return BadRequest(new { error = "display_name is required." });
        }

        var normalizedName = request.DisplayName.Trim().ToLower();

        // ── 3. Find the employee in the database ─────────────────────────
        var user = await _db.Users
            .OfType<ApplicationUser>()
            .FirstOrDefaultAsync(u =>
                u.DisplayName != null &&
                u.DisplayName.ToLower() == normalizedName);

        if (user == null)
        {
            _logger.LogWarning(
                "[ZendeskWebhook] Reset requested for '{Name}' (ticket #{Ticket}) but no matching user found.",
                request.DisplayName, request.TicketId);
            // Return 200 so Zendesk doesn't retry — this is a data issue, not a server error
            return Ok(new { status = "not_found", message = $"No employee found with name '{request.DisplayName}'." });
        }

        // ── 4. Reset back to first-time-login state ──────────────────────
        user.PasswordHash     = PlaceholderHash;
        user.IsFirstTimeLogin = true;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "[ZendeskWebhook] Password reset for '{Name}' (UserId: {Id}) via Zendesk ticket #{Ticket}.",
            user.DisplayName, user.Id, request.TicketId);

        return Ok(new { status = "reset", message = $"Password reset for {user.DisplayName}. They can now activate at /Account/Activate." });
    }
}

/// <summary>Payload sent by Zendesk when the trigger fires.</summary>
public sealed class ResetPasswordRequest
{
    /// <summary>The employee's full display name — must match DisplayName in AspNetUsers.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The Zendesk ticket ID — used only for logging/audit purposes.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("ticket_id")]
    public string TicketId { get; set; } = string.Empty;
}
