using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Policies;

public class ViewModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ViewModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public PolicyDocument? Item { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Item = await _db.PolicyDocuments.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (Item is null)
        {
            return NotFound();
        }

        return Page();
    }
}
