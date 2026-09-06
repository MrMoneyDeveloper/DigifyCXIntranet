using DigifyCXIntranet.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class BackupHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly BackupHealthOptions _options;

    public BackupHealthCheck(IConfiguration configuration, IOptions<BackupHealthOptions> options)
    {
        _configuration = configuration;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Backup health check is disabled.");
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("DefaultConnection is not configured.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = string.IsNullOrWhiteSpace(_options.DatabaseName)
            ? builder.InitialCatalog
            : _options.DatabaseName;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [type], DATEDIFF_BIG(SECOND, MAX(backup_finish_date), GETDATE())
            FROM msdb.dbo.backupset
            WHERE database_name = @databaseName
              AND [type] IN ('D', 'I', 'L')
            GROUP BY [type];

            SELECT recovery_model_desc
            FROM sys.databases
            WHERE [name] = @databaseName;
            """;
        command.Parameters.AddWithValue("@databaseName", databaseName);

        var ages = new Dictionary<string, TimeSpan>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ages[reader.GetString(0)] = TimeSpan.FromSeconds(reader.GetInt64(1));
        }

        var recoveryModel = "UNKNOWN";
        if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
        {
            recoveryModel = reader.GetString(0);
        }

        var evaluation = Evaluate(ages, recoveryModel, _options);

        var data = ages.ToDictionary(x => $"{BackupTypeName(x.Key)}Age", x => (object)x.Value);
        data["RecoveryModel"] = recoveryModel;
        if (evaluation.IsHealthy)
        {
            return HealthCheckResult.Healthy("Database backup freshness is within configured thresholds.", data);
        }

        return HealthCheckResult.Unhealthy("Database backup freshness is outside configured thresholds.", data: data);
    }

    internal static BackupFreshnessEvaluation Evaluate(
        IReadOnlyDictionary<string, TimeSpan> ages,
        string recoveryModel,
        BackupHealthOptions options)
    {
        var fullOk = IsFresh(ages, "D", TimeSpan.FromHours(options.MaxFullBackupAgeHours));
        var differentialThreshold = TimeSpan.FromHours(options.MaxDifferentialBackupAgeHours);
        var differentialOk = IsFresh(ages, "I", differentialThreshold) ||
                             IsFresh(ages, "D", differentialThreshold);
        var logRequired = string.Equals(recoveryModel, "FULL", StringComparison.OrdinalIgnoreCase);
        var logOk = !logRequired || IsFresh(ages, "L", TimeSpan.FromHours(options.MaxLogBackupAgeHours));
        return new BackupFreshnessEvaluation(fullOk, differentialOk, logOk, logRequired);
    }

    private static bool IsFresh(IReadOnlyDictionary<string, TimeSpan> ages, string type, TimeSpan maxAge) =>
        ages.TryGetValue(type, out var age) && age >= TimeSpan.Zero && age <= maxAge;

    private static string BackupTypeName(string type)
    {
        return type switch
        {
            "D" => "FullBackupUtc",
            "I" => "DifferentialBackupUtc",
            "L" => "LogBackupUtc",
            _ => type
        };
    }

    internal sealed record BackupFreshnessEvaluation(
        bool FullIsFresh,
        bool DifferentialIsFresh,
        bool LogIsFresh,
        bool LogIsRequired)
    {
        public bool IsHealthy => FullIsFresh && DifferentialIsFresh && LogIsFresh;
    }
}
