using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Controllers;

[ApiController]
[Route("api/zendesk")]
public class ZendeskWebhookController : ControllerBase
{
    private const string PlaceholderHash =
        "AQAAAAIAAYagAAAAEOf12WelcomeDigifyCX2024!Placeholder";

    private readonly ApplicationDbContext _db;
    private readonly ZendeskWebhookOptions _options;
    private readonly IAuditService _auditService;
    private readonly ILogger<ZendeskWebhookController> _logger;

    public ZendeskWebhookController(
        ApplicationDbContext db,
        IOptions<ZendeskWebhookOptions> options,
        IAuditService auditService,
        ILogger<ZendeskWebhookController> logger)
    {
        _db = db;
        _options = options.Value;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("zendesk-webhook")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("DigifyCX_Reset_Password_Secret", out var incomingSecret) ||
            !SecretsMatch(incomingSecret.ToString(), _options.Secret))
        {
            _logger.LogWarning("Zendesk password-reset webhook rejected because authentication failed.");
            return Unauthorized(new { error = "Invalid webhook secret." });
        }

        var normalizedName = request.DisplayName.Trim().ToLowerInvariant();
        var matchingUsers = await _db.Users
            .OfType<ApplicationUser>()
            .AsNoTracking()
            .Where(candidate => candidate.DisplayName != null && candidate.DisplayName.ToLower() == normalizedName)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchingUsers.Count != 1)
        {
            var ambiguous = matchingUsers.Count > 1;
            _logger.LogWarning(
                "Zendesk password-reset webhook could not uniquely match ticket {TicketId} to a user.",
                request.TicketId);
            await _auditService.WriteAsync(
                "zendesk",
                "PasswordResetRequested",
                "Account",
                $"ticket={request.TicketId}",
                succeeded: false,
                entityId: request.TicketId,
                errorCode: ambiguous ? "AmbiguousUser" : "UserNotFound",
                httpContext: HttpContext,
                cancellationToken: cancellationToken);
            return Ok(new
            {
                status = ambiguous ? "ambiguous" : "not_found",
                message = ambiguous
                    ? "Multiple employee accounts match. Contact IT support to identify the account."
                    : "No matching employee account was found."
            });
        }

        var user = matchingUsers[0];
        var newSecurityStamp = Guid.NewGuid().ToString();
        var newConcurrencyStamp = Guid.NewGuid().ToString();
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE [AspNetUsers] SET [PasswordHash] = {0}, [IsFirstTimeLogin] = 1, [SecurityStamp] = {1}, [ConcurrencyStamp] = {2} WHERE [Id] = {3}",
            new object[] { PlaceholderHash, newSecurityStamp, newConcurrencyStamp, user.Id },
            cancellationToken);

        _logger.LogInformation(
            "Zendesk password-reset webhook reset user {UserId} for ticket {TicketId}.",
            user.Id,
            request.TicketId);

        await _auditService.WriteAsync(
            "zendesk",
            "PasswordResetRequested",
            "Account",
            $"ticket={request.TicketId}",
            succeeded: true,
            entityId: user.Id,
            httpContext: HttpContext,
            cancellationToken: cancellationToken);

        return Ok(new { status = "reset", message = "The matching account can now be activated." });
    }

    internal static bool SecretsMatch(string? supplied, string? expected)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}

public sealed class ResetPasswordRequest
{
    [Required, MaxLength(120)]
    [System.Text.Json.Serialization.JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    [System.Text.Json.Serialization.JsonPropertyName("ticket_id")]
    public string TicketId { get; set; } = string.Empty;
}
