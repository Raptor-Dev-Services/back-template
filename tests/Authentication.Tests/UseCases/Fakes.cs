using Authentication.Application.Sessions;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Repositories;
using NSubstitute;
using Shared.Kernel.Context;

namespace Authentication.Tests.UseCases;

/// <summary>Unidad de trabajo que ejecuta el trabajo en linea y cuenta los guardados.</summary>
internal sealed class InlineUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) =>
        work(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal static class TestSessions
{
    public static readonly AuthOptions Options = new()
    {
        Key = "unit-tests-signing-key-0123456789abcdef-0123456789",
        Issuer = "tests",
        Audience = "tests",
    };

    public static SessionIssuer Create(IRbacRepository rbac, IRefreshTokenRepository refreshTokens)
    {
        var issuer = Substitute.For<IAccessTokenIssuer>();
        issuer.Issue(Arg.Any<AccessTokenSubject>()).Returns(new AccessToken("access-token", DateTime.UtcNow.AddMinutes(15)));
        rbac.GetGrantsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<string>)["Admin"], (IReadOnlyList<string>)["users.read"]));
        return new SessionIssuer(rbac, refreshTokens, issuer, Options);
    }
}
