using Azure.Core;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Controllers;

[ApiController]
[Route("api/zendesk")]
public class ZendeskWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly ZendeskWebhookOptions _options;
    private readonly ILogger<ZendeskWebhookController> _logger;

    private const string PlaceholderHash =
        "AQAAAAIAAYagAAAAEOf12WelcomeDigifyCX2024!Placeholder";

    public ZendeskWebhookController(
        ApplicationDbContext db,
        IPasswordHasher<ApplicationUser> hasher,
        IOptions<ZendeskWebhookOptions> options,
        ILogger<ZendeskWebhookController> logger)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        // ── DEBUG: log every incoming header so we can see exactly what Zendesk sends ──
        var allHeaders = string.Join(" | ", Request.Headers.Select(h => $"{h.Key}={h.Value}"));
        _logger.LogWarning("[ZendeskWebhook] Incoming headers: {Headers}", allHeaders);
        _logger.LogWarning("[ZendeskWebhook] Expected secret value: '{Secret}'", _options.Secret);
        // ── END DEBUG ──

        // ── 1. Validate the shared secret ────────────────────────────────
        if (!Request.Headers.TryGetValue("DigifyCX_Reset_Password_Secret", out var incomingSecret) ||
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
            return Ok(new { status = "not_found", message = $"No employee found with name '{request.DisplayName}'." });
        }

        // ── 4. Reset back to first-time-login state ──────────────────────
        user.PasswordHash = PlaceholderHash;
        user.IsFirstTimeLogin = true;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "[ZendeskWebhook] Password reset for '{Name}' (UserId: {Id}) via Zendesk ticket #{Ticket}.",
            user.DisplayName, user.Id, request.TicketId);

        return Ok(new { status = "reset", message = $"Password reset for {user.DisplayName}. They can now activate at /Account/Activate." });
    }
}

public sealed class ResetPasswordRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("ticket_id")]
    public string TicketId { get; set; } = string.Empty;
}