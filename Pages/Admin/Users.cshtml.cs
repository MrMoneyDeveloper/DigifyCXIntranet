using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class UsersModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public UsersModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
    }

    [BindProperty]
    public NewUserInput NewUser { get; set; } = new();

    public List<UserRow> Users { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; private set; }
    public IReadOnlyList<RoleOption> AssignableRoles { get; private set; } = Array.Empty<RoleOption>();
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        AssignableRoles = GetAssignableRoles();

        if (!AssignableRoles.Any(x => string.Equals(x.Value, NewUser.Role, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("NewUser.Role", "You are not allowed to assign that role.");
        }

        var username = BuildUsername(NewUser.FullName);
        if (!string.IsNullOrWhiteSpace(username))
        {
            var exists = await _db.Users
                .OfType<ApplicationUser>()
                .AnyAsync(u =>
                    (u.UserName != null && u.UserName.ToLower() == username) ||
                    (u.Email != null && u.Email.ToLower() == NewUser.Email.Trim().ToLower()));

            if (exists)
            {
                ModelState.AddModelError(string.Empty, "A user with that name or email already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = username,
            Email = NewUser.Email.Trim(),
            DisplayName = NewUser.FullName.Trim(),
            CustomRole = NewUser.Role,
            IsFirstTimeLogin = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            await _auditService.WriteAsync(
                UserNameHelper.GetShortName(User),
                "CreateFailed",
                "ApplicationUser",
                $"role={NewUser.Role}",
                succeeded: false,
                errorCode: "IdentityCreateFailed",
                httpContext: HttpContext);
            ErrorMessage = string.Join(" ", result.Errors.Select(x => x.Description));
            await LoadAsync();
            return Page();
        }

        await _auditService.WriteAsync(
            UserNameHelper.GetShortName(User),
            "Create",
            "ApplicationUser",
            $"role={user.CustomRole}",
            entityId: user.Id,
            httpContext: HttpContext);

        TempData["UserCreateSuccess"] = $"{user.DisplayName} was added. They can activate their account with the default password.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        AssignableRoles = GetAssignableRoles();
        SuccessMessage = TempData["UserCreateSuccess"] as string;
        PageSize = Math.Clamp(PageSize, 10, 100);
        TotalCount = await _db.Users.OfType<ApplicationUser>().CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, totalPages);

        Users = await _db.Users
            .OfType<ApplicationUser>()
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new UserRow(
                x.DisplayName,
                x.UserName ?? string.Empty,
                x.Email ?? string.Empty,
                NormalizeRole(x.CustomRole),
                x.IsFirstTimeLogin,
                !string.IsNullOrWhiteSpace(x.PasswordHash)))
            .ToListAsync();
    }

    private IReadOnlyList<RoleOption> GetAssignableRoles()
    {
        var roles = User.IsInRole(AppRoles.SuperAdmin)
            ? AppRoles.SuperAdminAssignableRoles
            : AppRoles.EmployeeAssignableRoles;

        return roles.Select(x => new RoleOption(x, ToLabel(x))).ToList();
    }

    private static string BuildUsername(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        return string.Join(".", trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ToLabel(string role) => role switch
    {
        AppRoles.HrAdmin => "HR Admin",
        AppRoles.CanteenAdmin => "Canteen Admin",
        AppRoles.FinanceAdmin => "Finance Admin",
        AppRoles.SystemAdmin => "System Admin",
        AppRoles.SuperAdmin => "Super Admin",
        _ => "Employee"
    };

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return AppRoles.Employee;
        }

        var candidate = role.Trim();
        return candidate is AppRoles.Employee
            or AppRoles.FinanceAdmin
            or AppRoles.HrAdmin
            or AppRoles.CanteenAdmin
            or AppRoles.SystemAdmin
            or AppRoles.SuperAdmin
            ? candidate
            : AppRoles.Employee;
    }

    public class NewUserInput
    {
        [Required]
        [MaxLength(120)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = AppRoles.Employee;
    }

    public record RoleOption(string Value, string Label);

    public record UserRow(
        string DisplayName,
        string Username,
        string Email,
        string Role,
        bool IsFirstTimeLogin,
        bool HasPassword);
}
