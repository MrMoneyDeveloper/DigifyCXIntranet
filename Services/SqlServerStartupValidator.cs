using Microsoft.Data.SqlClient;

namespace DigifyCXIntranet.Services;

public static class SqlServerStartupValidator
{
    public static async Task EnsureServerIsReachableAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        try
        {
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "SQL Server is not reachable. Install/start SQL Server LocalDB or update ConnectionStrings:DefaultConnection.");

            throw new InvalidOperationException(
                "SQL Server is not reachable. Install/start SQL Server LocalDB or update ConnectionStrings:DefaultConnection.",
                ex);
        }
    }
}
