using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public enum MealSlot
{
    Breakfast = 1,
    Lunch = 2
}

public class CanteenOrder
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string EmployeeUsername { get; set; } = string.Empty;

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    [Required]
    [MaxLength(300)]
    public string ItemSummary { get; set; } = string.Empty;

    public MealSlot MealSlot { get; set; } = MealSlot.Lunch;

    [Range(0, 100000)]
    public decimal TotalAmount { get; set; }

    public DateTime OrderTimeUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Submitted";

    public int? CanteenBatchRunId { get; set; }
    public CanteenBatchRun? CanteenBatchRun { get; set; }
}
