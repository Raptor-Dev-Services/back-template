using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Results;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Authorization;
using Users.Application.UseCases.DisableUserProfile;
using Users.Application.UseCases.GetUserProfile;
using Users.Application.UseCases.GetUserProfiles;
using Users.Application.UseCases.UpdateUserProfile;
using Users.Presentation.RequestBodies;

namespace Users.Presentation.Controllers;

/// <summary>
/// Perfiles de los usuarios del tenant del token. Cada accion declara su permiso: la prueba de superficie de
/// autorizacion falla si una nace solo con <c>[Authorize]</c>, que la abriria a cualquier usuario del tenant.
/// </summary>
[Route("api/v1/users")]
public sealed class UsersController(IMediator mediator, ResultViewModel<UsersController> viewModel) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersRead)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetUserProfilesRequest(page, pageSize), cancellationToken), viewModel);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetUserProfileRequest(id), cancellationToken), viewModel);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserProfileBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new UpdateUserProfileRequest(id, body.FullName), cancellationToken), viewModel);

    /// <summary>Baja reversible: el perfil queda inactivo, la credencial se apaga y sus sesiones se revocan.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersManage)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new DisableUserProfileRequest(CurrentUserPublicId, id), cancellationToken), viewModel);
}
