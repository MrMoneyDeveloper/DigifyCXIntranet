using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigifyCXIntranet.Services;

public static class SqlServerConnectionSecurity
{
    public static void Validate(string connectionString, IHostEnvironment environment, ILogger logger)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var isSecure = builder.Encrypt && !builder.TrustServerCertificate;

        if (isSecure)
        {
            return;
        }

        var message = "SQL Server connection should use Encrypt=True and TrustServerCertificate=False for trusted in-transit encryption.";
        if (environment.IsDevelopment())
        {
            logger.LogWarning("{Message} Current connection is allowed because the app is running in Development.", message);
            return;
        }

        throw new InvalidOperationException(message);
    }
}
