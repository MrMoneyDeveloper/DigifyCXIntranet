using Company.Product.Application;
using NetArchTest.Rules;

namespace Company.Product.Application.Tests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Domain_ShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(Company.Product.Domain.Orders.Order).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Company.Product.Infrastructure", "Company.Product.Api")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_ShouldNotDependOnApiOrInfrastructure()
    {
        var result = Types.InAssembly(typeof(DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Company.Product.Api", "Company.Product.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
