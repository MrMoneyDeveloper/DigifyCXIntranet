using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class MenuItem
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [MaxLength(20)]
    public string Emoji { get; set; } = "\U0001F37D";

    [MaxLength(80)]
    public string IconClass { get; set; } = string.Empty;

    [MaxLength(250)]
    public string ImagePath { get; set; } = string.Empty;

    public MealSlot MealSlot { get; set; } = MealSlot.Lunch;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public int DisplayOrder { get; set; }
}
