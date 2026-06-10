using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigifyCXIntranet.Pages.Admin;

public class PolicyEditModel : PageModel
{
    [BindProperty]
    public EditPolicyInput Item { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await Task.CompletedTask;
        return RedirectToPage("/Admin/Policies");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await Task.CompletedTask;
        return RedirectToPage("/Admin/Policies");
    }

    public class EditPolicyInput
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public PolicyContentType ContentType { get; set; }

        [Required]
        [MaxLength(30)]
        public string VersionLabel { get; set; } = string.Empty;

        [Required]
        public DateOnly EffectiveDate { get; set; }

        [Required]
        [MaxLength(8000)]
        public string Content { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
