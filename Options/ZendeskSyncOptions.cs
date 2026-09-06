using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class ZendeskSyncOptions
{
    public const string SectionName = "ZendeskSync";

    public string Subdomain { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Locale { get; set; } = "en-us";
    public bool UseApiToken { get; set; } = false;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    [Range(5, 90)]
    public int TimeoutSeconds { get; set; } = 20;
    [Range(1, 500)]
    public int MaxPages { get; set; } = 100;
    [Range(1024, 10 * 1024 * 1024)]
    public long MaxPageBytes { get; set; } = 2 * 1024 * 1024;
    public long InternalSupportTicketFormId { get; set; }
    public long HrGroupId { get; set; }
    public long RequesterEmailFieldId { get; set; }
    public long DepartmentFieldId { get; set; }
    public string HrDepartmentValue { get; set; } = "cxi_dept_hr";
    public long InquiryTypeFieldId { get; set; }
    public string RequestInquiryTypeValue { get; set; } = "cxi_type_request";
    public long UrgencyFieldId { get; set; }
    public string P2UrgencyValue { get; set; } = "cxi_p2";
    public long HrQueryTypeFieldId { get; set; }
    public string HrGeneralQueryValue { get; set; } = "cxi_hr_general";
    public long EmployeeFullNameFieldId { get; set; }
    public long EmployeeIdFieldId { get; set; }
    public long ManagerEmailFieldId { get; set; }
    public long RequestingOnBehalfOfFieldId { get; set; }

    /// <summary>
    /// Zendesk section IDs whose articles should be synced to the intranet.
    /// When empty, all articles are synced (no filtering).
    /// Add the Policies section ID here, plus any future signed-in-only sections.
    /// </summary>
    public List<long> AllowedSectionIds { get; set; } = new();
}
