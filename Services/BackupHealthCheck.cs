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
            SELECT [type], MAX(backup_finish_date)
            FROM msdb.dbo.backupset
            WHERE database_name = @databaseName
              AND [type] IN ('D', 'I', 'L')
            GROUP BY [type];
            """;
        command.Parameters.AddWithValue("@databaseName", databaseName);

        var latest = new Dictionary<string, DateTime>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            latest[reader.GetString(0)] = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
        }

        var now = DateTime.UtcNow;
        var fullOk = IsFresh(latest, "D", now, TimeSpan.FromHours(_options.MaxFullBackupAgeHours));
        var diffOk = !latest.ContainsKey("I") || IsFresh(latest, "I", now, TimeSpan.FromHours(_options.MaxDifferentialBackupAgeHours));
        var logOk = !latest.ContainsKey("L") || IsFresh(latest, "L", now, TimeSpan.FromHours(_options.MaxLogBackupAgeHours));

        var data = latest.ToDictionary(x => BackupTypeName(x.Key), x => (object)x.Value);
        if (fullOk && diffOk && logOk)
        {
            return HealthCheckResult.Healthy("Database backup freshness is within configured thresholds.", data);
        }

        return HealthCheckResult.Unhealthy("Database backup freshness is outside configured thresholds.", data: data);
    }

    private static bool IsFresh(IReadOnlyDictionary<string, DateTime> latest, string type, DateTime now, TimeSpan maxAge)
    {
        return latest.TryGetValue(type, out var timestamp) && now - timestamp.ToUniversalTime() <= maxAge;
    }

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
}
