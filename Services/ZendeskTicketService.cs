using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class ZendeskTicketService : IZendeskTicketService
{
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
Internal application submitted from DigifyCX Intranet.

Job: {job.Title}
Department: {job.Department}
Applicant: {employeeName}
Applicant email: {employeeEmail}
Employee ID: {employeeId}
Manager email: {managerEmail}

Notes:
{notes}
""";

        return CreateHrTicketAsync(subject, body, employeeName, employeeEmail, employeeName, employeeId, managerEmail, resumeFile, cancellationToken);
    }

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
        var subject = $"Referral application: {job.Title} - {candidateName}";
        var body = $"""
External referral submitted from DigifyCX Intranet.

Job: {job.Title}
Department: {job.Department}
Candidate: {candidateName}
Candidate email: {candidateEmail}
Candidate phone: {candidatePhone}
Referrer: {referrerName}
Referrer email: {referrerEmail}

Notes:
{notes}
""";

        return CreateHrTicketAsync(subject, body, candidateName, candidateEmail, candidateName, string.Empty, referrerEmail, resumeFile, cancellationToken);
    }

    private async Task<ZendeskTicketResult> CreateHrTicketAsync(
        string subject,
        string body,
        string requesterName,
        string requesterEmail,
        string employeeFullName,
        string employeeId,
        string managerEmail,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured(out var configurationError))
        {
            return new ZendeskTicketResult(false, null, string.Empty, configurationError);
        }

        try
        {
            var uploads = new List<string>();
            if (attachment is { Length: > 0 })
            {
                uploads.Add(await UploadAsync(attachment, cancellationToken));
            }

            var customFields = new List<object>
            {
                new { id = _options.RequesterEmailFieldId, value = requesterEmail },
                new { id = _options.DepartmentFieldId, value = _options.HrDepartmentValue },
                new { id = _options.InquiryTypeFieldId, value = _options.RequestInquiryTypeValue },
                new { id = _options.UrgencyFieldId, value = _options.P2UrgencyValue },
                new { id = _options.HrQueryTypeFieldId, value = _options.HrGeneralQueryValue },
                new { id = _options.EmployeeFullNameFieldId, value = employeeFullName },
                new { id = _options.RequestingOnBehalfOfFieldId, value = requesterEmail }
            };

            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                customFields.Add(new { id = _options.EmployeeIdFieldId, value = employeeId.Trim() });
            }

            if (!string.IsNullOrWhiteSpace(managerEmail))
            {
                customFields.Add(new { id = _options.ManagerEmailFieldId, value = managerEmail.Trim() });
            }

            var payload = new
            {
                ticket = new
                {
                    subject,
                    group_id = _options.HrGroupId,
                    ticket_form_id = _options.InternalSupportTicketFormId,
                    requester = new
                    {
                        name = string.IsNullOrWhiteSpace(requesterName) ? requesterEmail : requesterName,
                        email = requesterEmail
                    },
                    comment = new
                    {
                        body,
                        uploads
                    },
                    custom_fields = customFields,
                    tags = new[] { "digifycx_intranet", "hr_application" }
                }
            };

            using var request = CreateRequest(HttpMethod.Post, "/api/v2/tickets.json");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90)));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Zendesk ticket creation failed with status {StatusCode}.", response.StatusCode);
                return new ZendeskTicketResult(false, null, string.Empty, $"Zendesk ticket creation failed: {(int)response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var ticketId = doc.RootElement.GetProperty("ticket").GetProperty("id").GetInt64();
            var url = $"{_options.BaseUrl.TrimEnd('/')}/agent/tickets/{ticketId}";
            return new ZendeskTicketResult(true, ticketId, url, "Zendesk ticket created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zendesk ticket creation failed.");
            return new ZendeskTicketResult(false, null, string.Empty, ex.Message);
        }
    }

    private async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/v2/uploads.json?filename={Uri.EscapeDataString(file.FileName)}");
        await using var stream = file.OpenReadStream();
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.GetProperty("upload").GetProperty("token").GetString() ?? string.Empty;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_options.BaseUrl.TrimEnd('/')}{path}");
        var raw = $"{_options.Email}/token:{_options.ApiToken}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        return request;
    }

    private bool IsConfigured(out string message)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.Email) ||
            string.IsNullOrWhiteSpace(_options.ApiToken) ||
            _options.InternalSupportTicketFormId <= 0 ||
            _options.HrGroupId <= 0)
        {
            message = "Zendesk ticket integration is not fully configured.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
