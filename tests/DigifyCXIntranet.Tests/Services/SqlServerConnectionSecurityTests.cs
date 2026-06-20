using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigifyCXIntranet.Tests.Services;

public sealed class SqlServerConnectionSecurityTests
{
    [Fact]
    public void Validate_AllowsStrictConnectionString_InProduction()
    {
        var environment = new TestHostEnvironment("Production");

        var action = () => SqlServerConnectionSecurity.Validate(
            "Server=sql;Database=app;Encrypt=True;TrustServerCertificate=False",
            environment,
            NullLogger.Instance);

        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsTrustServerCertificate_InProduction()
    {
        var environment = new TestHostEnvironment("Production");

        var action = () => SqlServerConnectionSecurity.Validate(
            "Server=sql;Database=app;Encrypt=True;TrustServerCertificate=True",
            environment,
            NullLogger.Instance);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_AllowsWeakConnectionString_InDevelopment()
    {
        var environment = new TestHostEnvironment(Environments.Development);

        var action = () => SqlServerConnectionSecurity.Validate(
            "Server=(localdb)\\MSSQLLocalDB;Database=app;TrustServerCertificate=True",
            environment,
            NullLogger.Instance);

        action.Should().NotThrow();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
