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
}
