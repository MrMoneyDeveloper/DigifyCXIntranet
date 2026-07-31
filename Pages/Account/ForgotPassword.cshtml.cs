using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigifyCXIntranet.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly IZendeskTicketService _zendesk;
    private readonly ApplicationDbContext  _db;
    private readonly ILogger<ForgotPasswordModel> _logger;
    private readonly IAuditService _auditService;

    public ForgotPasswordModel(
        IZendeskTicketService zendesk,
        ApplicationDbContext  db,
        ILogger<ForgotPasswordModel> logger,
        IAuditService auditService)
    {
        _zendesk = zendesk;
        _db      = db;
        _logger  = logger;
        _auditService = auditService;
    }

    // ── View state ──────────────────────────────────────────────────
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>True once the form has been submitted (success or failure).</summary>
    public bool    Submitted     { get; private set; }
    public bool    Succeeded     { get; private set; }
    public long?   TicketId      { get; private set; }
    public string  ErrorMessage  { get; private set; } = string.Empty;

    // ── GET ──────────────────────────────────────────────────────────
    public void OnGet() { }

    // ── POST ─────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Capture IP server-side (invisible to the user on the form).
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString()
                 ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? "unknown";

        var now            = DateTime.UtcNow;
        var fullName       = Input.FullName.Trim();
        var autoDescription = $"User {fullName} has submitted a forgotten password request via the DigifyCX Intranet login page.";

        // Call Zendesk
        ZendeskTicketResult result;
        try
        {
            result = await _zendesk.CreateForgotPasswordTicketAsync(fullName, ip, now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error creating forgot-password Zendesk ticket for {User}", fullName);
            result = new ZendeskTicketResult(false, null, string.Empty, "An unexpected error occurred. Please contact IT directly.");
        }

        // Persist the audit record regardless of whether the Zendesk call succeeded.
        var record = new ForgotPasswordRequest
        {
            FullName         = fullName,
            Description      = autoDescription,
            IpAddress        = ip,
            SubmittedUtc     = now,
            ZendeskTicketId  = result.TicketId,
            ZendeskTicketUrl = result.TicketUrl ?? string.Empty,
            Succeeded        = result.Succeeded
        };

        try
        {
            _db.Set<ForgotPasswordRequest>().Add(record);
            await _db.SaveChangesAsync();
            await _auditService.WriteAsync(fullName, "ForgotPasswordSubmitted", "ForgotPasswordRequest", $"ticket={result.TicketId}", result.Succeeded, record.Id.ToString(), result.Succeeded ? "" : "ZendeskFailed", HttpContext);
        }
        catch (Exception ex)
        {
            // Non-fatal — log but don't block the user.
            _logger.LogError(ex, "Failed to persist ForgotPasswordRequest audit record for {User}", fullName);
        }

        Submitted    = true;
        Succeeded    = result.Succeeded;
        TicketId     = result.TicketId;
        ErrorMessage = result.Message;

        return Page();
    }

    // ── Input model ──────────────────────────────────────────────────
    public class InputModel
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [MaxLength(120, ErrorMessage = "Name must be 120 characters or fewer.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;
    }
}
