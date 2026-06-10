using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

/// <summary>
/// Creates Zendesk HR tickets for internal job applications and employee
/// referrals.  All ticket-routing IDs and custom-field values are
/// hard-coded here so tickets always land in the correct HR group,
/// form, and queue — independent of appsettings.
/// </summary>
public class ZendeskTicketService : IZendeskTicketService
{
    // ----------------------------------------------------------------
    // Hard-coded Zendesk HR ticket constants
    // Form: CXI — Internal Support & Requests
    private const long TicketFormId = 22989127409436L;
    // Group: CXI — HR People Ops
    private const long HrGroupId = 22708167587228L;

    // Custom field IDs
    private const long FieldRequesterEmail       = 22988070511644L;
    private const long FieldDepartment           = 22964977267612L;
    private const long FieldInquiryType          = 22964896825500L;
    private const long FieldUrgency              = 22965145372572L;
    private const long FieldHrQueryType          = 22729769116956L;
    private const long FieldEmployeeFullName     = 22729924058012L;
    private const long FieldEmployeeId           = 22729955119004L;
    private const long FieldManagerEmail         = 22971445823900L;
    private const long FieldRequestingOnBehalfOf = 24644803197724L;

    // Custom field values
    private const string ValDepartment   = "cxi_dept_hr";
    private const string ValInquiryType  = "cxi_type_request";
    private const string ValUrgency      = "cxi_p2";
    private const string ValHrQueryType  = "cxi_hr_general";
    // ----------------------------------------------------------------

    private readonly HttpClient _httpClient;
    private readonly ZendeskSyncOptions _options;
    private readonly ILogger<ZendeskTicketService> _logger;

    public ZendeskTicketService(
        IHttpClientFactory httpClientFactory,
        IOptions<ZendeskSyncOptions> options,
        ILogger<ZendeskTicketService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(ZendeskTicketService));
        _options = options.Value;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Internal application
    // Requester = signed-in employee email.
    // Ticket body contains job details, employee info, notes and CV.
    // ------------------------------------------------------------------
    public Task<ZendeskTicketResult> CreateInternalApplicationTicketAsync(
        JobPosting job,
        string employeeName,
        string employeeEmail,
        string employeeId,
        string managerEmail,
        string notes,
        IFormFile? resumeFile,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Internal application: {job.Title}";
        var body = $"""
Internal job application submitted via DigifyCX Intranet.

Job Title    : {job.Title}
Department   : {job.Department}

Employee Name  : {employeeName}
Employee Email : {employeeEmail}
Employee ID    : {(string.IsNullOrWhiteSpace(employeeId) ? "(not provided)" : employeeId)}
Manager Email  : {(string.IsNullOrWhiteSpace(managerEmail) ? "(not provided)" : managerEmail)}

Notes:
{(string.IsNullOrWhiteSpace(notes) ? "(none)" : notes)}
""";

        return CreateHrTicketCoreAsync(
            subject:           subject,
            body:              body,
            requesterName:     employeeName,
            requesterEmail:    employeeEmail,
            employeeFullName:  employeeName,
            employeeId:        employeeId,
            managerEmail:      managerEmail,
            onBehalfOfEmail:   employeeEmail,
            attachment:        resumeFile,
            tags:              new[] { "digifycx_intranet", "hr_internal_application" },
            cancellationToken: cancellationToken);
    }

    // ------------------------------------------------------------------
    // Employee referral
    // Requester = candidate email (so Zendesk ticket is opened for them).
    // Ticket body contains referrer details and job details.
    // ------------------------------------------------------------------
    public Task<ZendeskTicketResult> CreateReferralTicketAsync(
        JobPosting job,
        string referrerName,
        string referrerEmail,
        string candidateName,
        string candidateEmail,
        string candidatePhone,
        string notes,
        IFormFile? resumeFile,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Referral application: {job.Title} — {candidateName}";
        var body = $"""
External referral submitted via DigifyCX Intranet.

Job Title    : {job.Title}
Department   : {job.Department}

Candidate Name  : {candidateName}
Candidate Email : {candidateEmail}
Candidate Phone : {(string.IsNullOrWhiteSpace(candidatePhone) ? "(not provided)" : candidatePhone)}

Referred by     : {referrerName}
Referrer Email  : {referrerEmail}

Notes:
{(string.IsNullOrWhiteSpace(notes) ? "(none)" : notes)}
""";

        return CreateHrTicketCoreAsync(
            subject:           subject,
            body:              body,
            requesterName:     candidateName,
            requesterEmail:    candidateEmail,
            employeeFullName:  candidateName,
            employeeId:        string.Empty,
            managerEmail:      referrerEmail,    // referrer email in manager field for HR routing
            onBehalfOfEmail:   referrerEmail,
            attachment:        resumeFile,
            tags:              new[] { "digifycx_intranet", "hr_referral" },
            cancellationToken: cancellationToken);
    }

    // ------------------------------------------------------------------
    // Core ticket creator — all HR tickets go through here.
    // All routing IDs and custom-field values are sourced from the
    // hard-coded constants at the top of this file.
    // ------------------------------------------------------------------
    private async Task<ZendeskTicketResult> CreateHrTicketCoreAsync(
        string subject,
        string body,
        string requesterName,
        string requesterEmail,
        string employeeFullName,
        string employeeId,
        string managerEmail,
        string onBehalfOfEmail,
        IFormFile? attachment,
        string[] tags,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured(out var configError))
        {
            return new ZendeskTicketResult(false, null, string.Empty, configError);
        }

        try
        {
            // 1. Upload CV / resume if provided
            var uploads = new List<string>();
            if (attachment is { Length: > 0 })
            {
                uploads.Add(await UploadAsync(attachment, cancellationToken));
            }

            // 2. Build custom fields — always include required fields;
            //    optional fields only when non-empty.
            var customFields = new List<object>
            {
                new { id = FieldRequesterEmail,       value = requesterEmail },
                new { id = FieldDepartment,           value = ValDepartment },
                new { id = FieldInquiryType,          value = ValInquiryType },
                new { id = FieldUrgency,              value = ValUrgency },
                new { id = FieldHrQueryType,          value = ValHrQueryType },
                new { id = FieldEmployeeFullName,     value = employeeFullName },
                new { id = FieldRequestingOnBehalfOf, value = onBehalfOfEmail }
            };

            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                customFields.Add(new { id = FieldEmployeeId, value = employeeId.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(managerEmail))
            {
                customFields.Add(new { id = FieldManagerEmail, value = managerEmail.Trim() });
            }

            // 3. Assemble the full ticket payload
            var payload = new
            {
                ticket = new
                {
                    subject,
                    ticket_form_id = TicketFormId,
                    group_id       = HrGroupId,
                    requester = new
                    {
                        name  = string.IsNullOrWhiteSpace(requesterName) ? requesterEmail : requesterName,
                        email = requesterEmail
                    },
                    comment = new
                    {
                        body,
                        uploads
                    },
                    custom_fields = customFields,
                    tags
                }
            };

            // 4. POST to Zendesk
            using var request = BuildRequest(HttpMethod.Post, "/api/v2/tickets.json");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90)));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            var responseBody  = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Zendesk ticket creation failed. Status={Status} Body={Body}",
                    response.StatusCode, responseBody);
                return new ZendeskTicketResult(
                    false, null, string.Empty,
                    $"Zendesk returned {(int)response.StatusCode}.");
            }

            using var doc     = JsonDocument.Parse(responseBody);
            var ticketId      = doc.RootElement.GetProperty("ticket").GetProperty("id").GetInt64();
            var ticketUrl     = $"{_options.BaseUrl.TrimEnd('/')}/agent/tickets/{ticketId}";

            return new ZendeskTicketResult(true, ticketId, ticketUrl, "Ticket created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zendesk ticket creation threw an exception.");
            return new ZendeskTicketResult(false, null, string.Empty, ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // Upload a file to Zendesk and return the upload token.
    // ------------------------------------------------------------------
    private async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(
            HttpMethod.Post,
            $"/api/v2/uploads.json?filename={Uri.EscapeDataString(file.FileName)}");

        await using var stream = file.OpenReadStream();
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc    = JsonDocument.Parse(responseBody);
        return doc.RootElement
                   .GetProperty("upload")
                   .GetProperty("token")
                   .GetString() ?? string.Empty;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_options.BaseUrl.TrimEnd('/')}{path}");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Email}/token:{_options.ApiToken}"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private bool IsConfigured(out string message)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.Email) ||
            string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            message = "Zendesk integration is not configured (BaseUrl / Email / ApiToken missing).";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
