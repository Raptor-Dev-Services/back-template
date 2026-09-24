using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Web;
using Users.Application.UseCases.DisableUserProfile;
using Users.Application.UseCases.GetUserProfile;
using Users.Application.UseCases.GetUserProfiles;
using Users.Application.UseCases.UpdateUserProfile;
using Users.Presentation.RequestBodies;

namespace Users.Presentation;

[Route("api/v1/users")]
[Authorize]
public sealed class UsersController(IMediator mediator, ResultViewModel<UsersController> viewModel)
    : BaseApiController(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(new GetUserProfilesRequest(page, pageSize), cancellationToken);
        return MapResult(response, viewModel);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(new GetUserProfileRequest(id), cancellationToken);
        return MapResult(response, viewModel);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserProfileBody body, CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(new UpdateUserProfileRequest(id, body.FullName), cancellationToken);
        return MapResult(response, viewModel);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(new DisableUserProfileRequest(id), cancellationToken);
        return MapResult(response, viewModel);
    }
}
