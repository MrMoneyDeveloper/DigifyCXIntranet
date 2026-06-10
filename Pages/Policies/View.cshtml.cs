using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Policies;

public class ViewModel : PageModel
{
    private readonly PolicyDbContext _db;

    public ViewModel(PolicyDbContext db)
    {
        _db = db;
    }

    public ZendeskPolicyArticle? Item { get; private set; }
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
        var existing = await _db.PolicyAcknowledgements.FirstOrDefaultAsync(x =>
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
        Item = await _db.ZendeskPolicyArticles.FirstOrDefaultAsync(x => x.Id == id && x.IsPublished);
        if (Item is null)
        {
            return;
        }

        var employee = UserNameHelper.GetShortName(User);
        AlreadyAcknowledged = await _db.PolicyAcknowledgements.AnyAsync(x =>
            x.EmployeeDomainName == employee &&
            x.PolicyArticleId == Item.ZendeskArticleId &&
            x.PolicyVersion == Item.VersionLabel);
    }
}
