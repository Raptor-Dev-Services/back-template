using NetArchTest.Rules;
using System.Reflection;
using Users.Application.UseCases.GetUserProfile;
using Users.Domain.Entities;
using Users.Infrastructure.Repositories;
using Xunit;

namespace Users.Tests.Architecture;

public sealed class UsersArchitectureTests
{
    private const string ApplicationNs    = "Users.Application";
    private const string InfrastructureNs = "Users.Infrastructure";
    private const string PresentationNs   = "Users.Presentation";

    private static readonly Assembly DomainAssembly         = typeof(UserProfile).Assembly;
    private static readonly Assembly ApplicationAssembly    = typeof(GetUserProfileHandler).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(UserProfileRepository).Assembly;

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
