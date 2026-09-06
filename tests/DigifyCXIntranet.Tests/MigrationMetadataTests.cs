using DigifyCXIntranet.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DigifyCXIntranet.Tests;

public class MigrationMetadataTests
{
    [Fact]
    public void OperationalHardeningMigration_IsDiscoverableByEntityFramework()
    {
        var attribute = typeof(AddOperationalHardening)
            .GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
            .Cast<MigrationAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.Id.Should().Be("20260620143000_AddOperationalHardening");
    }
}
