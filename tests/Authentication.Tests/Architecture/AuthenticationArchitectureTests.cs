using Authentication.Application.UseCases.Login;
using Authentication.Domain.Entities;
using Authentication.Infrastructure.Repositories;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Authentication.Tests.Architecture;

public sealed class AuthenticationArchitectureTests
{
    private const string ApplicationNs    = "Authentication.Application";
    private const string InfrastructureNs = "Authentication.Infrastructure";
    private const string PresentationNs   = "Authentication.Presentation";

    private static readonly Assembly DomainAssembly         = typeof(UserCredential).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(LoginHandler).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(UserCredentialRepository).Assembly;

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
