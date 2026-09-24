using NSubstitute;
using Users.Application.UseCases.GetUserProfile;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Domain.Entities;
using Users.Domain.Repositories;
using Xunit;

namespace Users.Tests.UseCases;

public sealed class GetUserProfileHandlerTests
{
    private readonly IUserProfileRepository _repo = Substitute.For<IUserProfileRepository>();

    [Fact]
    public async Task Handle_WhenProfileExists_ReturnsSuccess()
    {
        var profile = new UserProfile
        {
            PublicId     = Guid.NewGuid(),
            TenantId     = 1,
            FullName     = "John Doe",
            IsActive     = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _repo.GetByPublicIdAsync(profile.PublicId, 1, Arg.Any<CancellationToken>()).Returns(profile);

        var result = await new GetUserProfileHandler(_repo)
            .Handle(new GetUserProfileRequest(profile.PublicId, 1), default);

        var success = Assert.IsType<GetUserProfileSuccess>(result);
        Assert.NotNull(success.Data);
        Assert.Equal("John Doe", success.Data.FullName);
    }

    [Fact]
    public async Task Handle_WhenProfileNotFound_ReturnsNotFoundFailure()
    {
        _repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((UserProfile?)null);

        var result = await new GetUserProfileHandler(_repo)
            .Handle(new GetUserProfileRequest(Guid.NewGuid(), 1), default);

        Assert.IsType<GetUserProfileNotFoundFailure>(result);
    }
}
