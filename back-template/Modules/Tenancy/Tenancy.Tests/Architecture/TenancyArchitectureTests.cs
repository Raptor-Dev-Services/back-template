using NetArchTest.Rules;
using System.Reflection;
using Tenancy.Application.Api;
using Tenancy.Domain.Entities;
using Tenancy.Infrastructure.Repositories;
using Xunit;

namespace Tenancy.Tests.Architecture;

public sealed class TenancyArchitectureTests
{
    private const string ApplicationNs    = "Tenancy.Application";
    private const string InfrastructureNs = "Tenancy.Infrastructure";
    private const string PresentationNs   = "Tenancy.Presentation";

    private static readonly Assembly DomainAssembly         = typeof(Tenant).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(TenancyApi).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(TenantRepository).Assembly;

    [Fact]
    public void Domain_MustNot_DependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Domain_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Domain_MustNot_DependOn_Presentation()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(PresentationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_MustNot_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_MustNot_DependOn_Presentation()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(PresentationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_MustNot_DependOn_Application()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_MustNot_DependOn_Presentation()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(PresentationNs)
            .GetResult();
        Assert.True(result.IsSuccessful);
    }
}
