using System.ComponentModel.DataAnnotations;

namespace Company.Product.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionStringName { get; init; } = "SqlServer";

    public bool RunMigrationsOnStartup { get; init; }
}
