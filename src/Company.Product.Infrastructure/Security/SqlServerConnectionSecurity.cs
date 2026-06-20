using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Company.Product.Infrastructure.Security;

public static class SqlServerConnectionSecurity
{
    public static void Validate(string connectionString, bool isDevelopment, ILogger logger)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var isSecure = builder.Encrypt && !builder.TrustServerCertificate;

        if (isSecure)
        {
            return;
        }

        var message = "SQL Server connection should use Encrypt=True and TrustServerCertificate=False for trusted in-transit encryption.";
        if (isDevelopment)
        {
            logger.LogWarning("{Message} Current connection is allowed because the app is running in Development.", message);
            return;
        }

        throw new InvalidOperationException(message);
    }
}
