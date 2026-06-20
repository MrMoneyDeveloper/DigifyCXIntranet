using Company.Product.Application;
using Company.Product.Infrastructure;
using Company.Product.Infrastructure.Options;
using Company.Product.Infrastructure.Security;
using Company.Product.WorkerService.Jobs;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OperationalHeartbeatWorker>();

var host = builder.Build();
var databaseOptions = host.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
var connectionString = builder.Configuration.GetConnectionString(databaseOptions.ConnectionStringName);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException($"Connection string '{databaseOptions.ConnectionStringName}' was not found.");
}

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SqlServerConnectionSecurity");
SqlServerConnectionSecurity.Validate(connectionString, builder.Environment.IsDevelopment(), logger);
host.Run();
