using Company.Product.Application;
using Company.Product.Infrastructure;
using Company.Product.WorkerService.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OperationalHeartbeatWorker>();

var host = builder.Build();
host.Run();
