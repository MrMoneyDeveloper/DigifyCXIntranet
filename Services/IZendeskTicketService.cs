using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public interface IZendeskTicketService
{
    Task<ZendeskTicketResult> CreateInternalApplicationTicketAsync(
        JobPosting job,
        string employeeName,
        string employeeEmail,
        string employeeId,
        string managerEmail,
        string notes,
        IFormFile? resumeFile,
        CancellationToken cancellationToken = default);

    Task<ZendeskTicketResult> CreateReferralTicketAsync(
        JobPosting job,
        string referrerName,
        string referrerEmail,
        string candidateName,
        string candidateEmail,
        string candidatePhone,
        string notes,
        IFormFile? resumeFile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Zendesk IT-support ticket for a forgotten-password request.
    /// The ticket body automatically includes the username, IP address, and
    /// submission timestamp so IT can locate the correct device on the floor.
    /// The ticket is tagged with <c>digifycx_intranet_forgot_password</c>.
    /// </summary>
    Task<ZendeskTicketResult> CreateForgotPasswordTicketAsync(
        string fullName,
        string ipAddress,
        DateTime submittedUtc,
        CancellationToken cancellationToken = default);
}
