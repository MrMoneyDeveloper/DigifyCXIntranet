using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

/// <summary>
/// Creates Zendesk tickets for HR workflows (internal applications, referrals)
/// and IT-support workflows (forgotten password).
/// </summary>
public class ZendeskTicketService : IZendeskTicketService
{
    // ----------------------------------------------------------------
    // Hard-coded Zendesk HR ticket constants
    // ----------------------------------------------------------------
    private const long   TicketFormId             = 22989127409436L;
    private const long   HrGroupId                = 22708167587228L;

    private const long   FieldRequesterEmail       = 22988070511644L;
    private const long   FieldDepartment           = 22964977267612L;
    private const long   FieldInquiryType          = 22964896825500L;
    private const long   FieldUrgency              = 22965145372572L;
    private const long   FieldHrQueryType          = 22729769116956L;
    private const long   FieldEmployeeFullName     = 22729924058012L;
    private const long   FieldEmployeeId           = 22729955119004L;
    private const long   FieldRequestingOnBehalfOf = 24644803197724L;

    private const string ValDepartment  = "cxi_dept_hr";
    private const string ValInquiryType = "cxi_type_request";
    private const string ValUrgency     = "cxi_p2";
    private const string ValHrQueryType = "cxi_hr_general";
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
        _options    = options.Value;
        _logger     = logger;
    }

    // ------------------------------------------------------------------
    // Internal application — requester = signed-in employee
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
Employee ID    : {(string.IsNullOrWhiteSpace(employeeId)  ? "(not provided)" : employeeId)}
Manager Email  : {(string.IsNullOrWhiteSpace(managerEmail) ? "(not provided)" : managerEmail)}

Notes:
{(string.IsNullOrWhiteSpace(notes) ? "(none)" : notes)}
""";

        return CreateHrTicketCoreAsync(
            subject, body,
            requesterName:    employeeName,
            requesterEmail:   employeeEmail,
            employeeFullName: employeeName,
            employeeId:       employeeId,
            onBehalfOfEmail:  employeeEmail,
            attachment:       resumeFile,
            tags:             new[] { "digifycx_intranet", "hr_internal_application" },
            cancellationToken: cancellationToken);
    }

    // ------------------------------------------------------------------
    // Referral — requester = referrer (employee), NOT the external candidate.
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
        var subject = $"Referral: {job.Title} \u2014 {candidateName}";
        var body = $"""
External referral submitted via DigifyCX Intranet.

Job Title    : {job.Title}
Department   : {job.Department}

Candidate Name  : {candidateName}
Candidate Email : {candidateEmail}
Candidate Phone : {(string.IsNullOrWhiteSpace(candidatePhone) ? "(not provided)" : candidatePhone)}

Referred by    : {referrerName}
Referrer Email : {referrerEmail}

Notes:
{(string.IsNullOrWhiteSpace(notes) ? "(none)" : notes)}
""";

        return CreateHrTicketCoreAsync(
            subject, body,
            requesterName:    referrerName,
            requesterEmail:   referrerEmail,
            employeeFullName: referrerName,
            employeeId:       string.Empty,
            onBehalfOfEmail:  referrerEmail,
            attachment:       resumeFile,
            tags:             new[] { "digifycx_intranet", "hr_referral" },
            cancellationToken: cancellationToken);
    }

    // ------------------------------------------------------------------
    // Forgotten password — IT-support ticket
    // ------------------------------------------------------------------
    public async Task<ZendeskTicketResult> CreateForgotPasswordTicketAsync(
        string fullName,
        string ipAddress,
        DateTime submittedUtc,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(out var configError))
            return Fail(configError);

        // Format the timestamp in South African time (SAST = UTC+2) for readability.
        var sastZone     = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        var sastTime     = TimeZoneInfo.ConvertTimeFromUtc(submittedUtc, sastZone);
        var timestampStr = sastTime.ToString("yyyy-MM-dd HH:mm:ss") + " SAST";

        var subject = $"Forgotten Password \u2013 {fullName}";
        var autoDescription = $"User {fullName} has submitted a forgotten password request via the DigifyCX Intranet login page.";

        var body = $"""
Forgotten password request submitted via DigifyCX Intranet.

Username  : {fullName}
IP Address: {ipAddress}
Timestamp : {timestampStr}

{autoDescription}

Please locate the device matching the IP address above on the company floor and assist the employee with a password reset.
""";

        try
        {
            // FieldEmployeeFullName is populated so the Zendesk webhook macro can
            // read {{ticket.ticket_field_22729924058012}} and pass the exact name
            // back to the ZendeskWebhookController for the DB lookup.
            var payload = new
            {
                ticket = new
                {
                    subject,
                    requester = new
                    {
                        name  = fullName,
                        email = _options.Email    // authenticated agent submits on behalf of user
                    },
                    comment = new { body },
                    custom_fields = new[]
                    {
                        new { id = FieldEmployeeFullName, value = fullName }
                    },
                    tags = new[] { "digifycx_intranet", "digifycx_intranet_forgot_password" }
                }
            };

            using var request = BuildRequest(HttpMethod.Post, "/api/v2/tickets.json");
            request.Content   = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90)));

            using var response     = await _httpClient.SendAsync(request, cts.Token);
            var       responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = BuildDetailedErrorMessage((int)response.StatusCode, response.ReasonPhrase, responseBody);
                _logger.LogWarning(
                    "Zendesk forgot-password ticket creation failed. Status={Status} User={User} Error={Error}",
                    (int)response.StatusCode, fullName, errorMsg);
                return Fail(errorMsg);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var ticketId  = doc.RootElement.GetProperty("ticket").GetProperty("id").GetInt64();
            var ticketUrl = $"{_options.BaseUrl.TrimEnd('/')}/agent/tickets/{ticketId}";

            _logger.LogInformation(
                "Zendesk forgot-password ticket #{TicketId} created for user {User} from IP {IP}.",
                ticketId, fullName, ipAddress);

            return new ZendeskTicketResult(true, ticketId, ticketUrl, "Ticket created.");
        }
        catch (TaskCanceledException)
        {
            const string msg = "Zendesk request timed out. The ticket may not have been created.";
            _logger.LogWarning(msg);
            return Fail(msg);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error reaching Zendesk: {Message}", ex.Message);
            return Fail($"Network error reaching Zendesk: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Zendesk forgot-password ticket creation: {Message}", ex.Message);
            return Fail($"Unexpected error: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Core — builds and POSTs an HR ticket, surfaces detailed errors
    // ------------------------------------------------------------------
    private async Task<ZendeskTicketResult> CreateHrTicketCoreAsync(
        string subject,
        string body,
        string requesterName,
        string requesterEmail,
        string employeeFullName,
        string employeeId,
        string onBehalfOfEmail,
        IFormFile? attachment,
        string[] tags,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured(out var configError))
            return Fail(configError);

        try
        {
            var uploads = new List<string>();
            if (attachment is { Length: > 0 })
                uploads.Add(await UploadAsync(attachment, cancellationToken));

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
                customFields.Add(new { id = FieldEmployeeId, value = employeeId.Trim() });

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
                    comment = new { body, uploads },
                    custom_fields = customFields,
                    tags
                }
            };

            using var request = BuildRequest(HttpMethod.Post, "/api/v2/tickets.json");
            request.Content   = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90)));

            using var response     = await _httpClient.SendAsync(request, cts.Token);
            var       responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = BuildDetailedErrorMessage((int)response.StatusCode, response.ReasonPhrase, responseBody);
                _logger.LogWarning(
                    "Zendesk ticket creation failed. Status={Status} Subject={Subject} Error={Error} Body={Body}",
                    (int)response.StatusCode, subject, errorMsg, responseBody);
                return Fail(errorMsg);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var ticketId  = doc.RootElement.GetProperty("ticket").GetProperty("id").GetInt64();
            var ticketUrl = $"{_options.BaseUrl.TrimEnd('/')}/agent/tickets/{ticketId}";

            _logger.LogInformation("Zendesk ticket #{TicketId} created ({Url}).", ticketId, ticketUrl);
            return new ZendeskTicketResult(true, ticketId, ticketUrl, "Ticket created.");
        }
        catch (TaskCanceledException)
        {
            const string msg = "Zendesk request timed out. The ticket may not have been created.";
            _logger.LogWarning(msg);
            return Fail(msg);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error reaching Zendesk: {Message}", ex.Message);
            return Fail($"Network error reaching Zendesk: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Zendesk ticket creation: {Message}", ex.Message);
            return Fail($"Unexpected error: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Parses Zendesk error response body into a human-readable message.
    // ------------------------------------------------------------------
    private static string BuildDetailedErrorMessage(int statusCode, string? reason, string responseBody)
    {
        try
        {
            using var errDoc = JsonDocument.Parse(responseBody);
            var root = errDoc.RootElement;

            var topLevel = root.TryGetProperty("description", out var desc) ? desc.GetString()
                         : root.TryGetProperty("error",       out var err)  ? err.GetString()
                         : null;

            var fieldErrors = new List<string>();
            if (root.TryGetProperty("details", out var details) &&
                details.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in details.EnumerateObject())
                {
                    if (field.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var item in field.Value.EnumerateArray())
                    {
                        var fieldDesc = item.TryGetProperty("description", out var fd)
                            ? fd.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(fieldDesc))
                            fieldErrors.Add($"  [{field.Name}] {fieldDesc}");
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append($"HTTP {statusCode} ({reason})");
            if (!string.IsNullOrWhiteSpace(topLevel))
                sb.Append($": {topLevel}");
            if (fieldErrors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Field validation errors:");
                foreach (var fe in fieldErrors)
                    sb.AppendLine(fe);
            }
            return sb.ToString().TrimEnd();
        }
        catch
        {
            return $"HTTP {statusCode} ({reason}): {responseBody[..Math.Min(400, responseBody.Length)]}";
        }
    }

    // ------------------------------------------------------------------
    // Upload a file and return the upload token
    // ------------------------------------------------------------------
    private async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(
            HttpMethod.Post,
            $"/api/v2/uploads.json?filename={Uri.EscapeDataString(SafeFileNames.Normalize(file.FileName, "attachment.bin"))}");

        await using var stream = file.OpenReadStream();
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("upload").GetProperty("token").GetString() ?? string.Empty;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var request     = new HttpRequestMessage(method, $"{_options.BaseUrl.TrimEnd('/')}{path}");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Email}/token:{_options.ApiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private bool IsConfigured(out string message)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.Email)   ||
            string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            message = "Zendesk is not configured on this server (BaseUrl / Email / ApiToken missing in appsettings). Contact your system administrator.";
            return false;
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            message = "Zendesk BaseUrl must be an absolute HTTPS URL.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static ZendeskTicketResult Fail(string message) =>
        new(false, null, string.Empty, message);
}
