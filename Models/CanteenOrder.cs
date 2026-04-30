using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class CanteenOrder
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string EmployeeUsername { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string ItemSummary { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0, 100000)]
    public decimal TotalAmount { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Submitted";

    [Range(0, 120)]
    public int EmploymentMonthsAtOrder { get; set; }

    public bool IncludedInPayrollReconciliation { get; set; } = true;
}
