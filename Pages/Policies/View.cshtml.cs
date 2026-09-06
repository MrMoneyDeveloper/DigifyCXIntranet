using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Policies;

public class ViewModel : PageModel
{
    private readonly PolicyDbContext _db;
    private readonly ZendeskSyncOptions _zendeskSyncOptions;
    private readonly IZendeskHtmlSanitizer _htmlSanitizer;

    public ViewModel(
        PolicyDbContext db,
        IOptions<ZendeskSyncOptions> zendeskSyncOptions,
        IZendeskHtmlSanitizer htmlSanitizer)
    {
        _db = db;
        _zendeskSyncOptions = zendeskSyncOptions.Value;
        _htmlSanitizer = htmlSanitizer;
    }

    public ZendeskPolicyArticle? Item { get; private set; }
    public string SanitizedBody { get; private set; } = string.Empty;
    public string? SafeHtmlUrl { get; private set; }
    public bool AlreadyAcknowledged { get; private set; }

    [TempData]
    public string AcknowledgeMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadAsync(id);
        if (Item is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(int id)
    {
        await LoadAsync(id);
        if (Item is null)
        {
            return NotFound();
        }

        var employee = UserNameHelper.GetShortName(User);
        var existing = await _db.PolicyAcknowledgements.AsNoTracking().FirstOrDefaultAsync(x =>
            x.EmployeeDomainName == employee &&
            x.PolicyArticleId == Item.ZendeskArticleId &&
            x.PolicyVersion == Item.VersionLabel);

        if (existing is null)
        {
            _db.PolicyAcknowledgements.Add(new PolicyAcknowledgement
            {
                EmployeeDomainName = employee,
                PolicyArticleId = Item.ZendeskArticleId,
                PolicyVersion = Item.VersionLabel,
                TimestampUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        AcknowledgeMessage = "Acknowledgement recorded.";
        return RedirectToPage(new { id });
    }

    private async Task LoadAsync(int id)
    {
        Item = await _db.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsPublished)
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .FirstOrDefaultAsync();
        if (Item is null)
        {
            return;
        }

        SanitizedBody = _htmlSanitizer.Sanitize(Item.Body);
        SafeHtmlUrl = _htmlSanitizer.SanitizeHttpsUrl(Item.HtmlUrl);

        var employee = UserNameHelper.GetShortName(User);
        AlreadyAcknowledged = await _db.PolicyAcknowledgements.AsNoTracking().AnyAsync(x =>
            x.EmployeeDomainName == employee &&
            x.PolicyArticleId == Item.ZendeskArticleId &&
            x.PolicyVersion == Item.VersionLabel);
    }
}
