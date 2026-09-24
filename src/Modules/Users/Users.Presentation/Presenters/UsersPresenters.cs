using Common.ViewModels;
using Shared.Kernel.Results;
using Shared.Web;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Contracts.Dtos;
using Users.Presentation.Controllers;

namespace Users.Presentation.Presenters;

// Un presenter por caso de uso, registrado a mano en ServiceCollectionEx. El mapeo comun vive en ResultPresenter.

internal sealed class GetUserProfilePresenter(ResultViewModel<UsersController> vm)
    : ResultPresenter<UsersController, GetUserProfileResponse, UserProfileDto>(vm);

internal sealed class GetUserProfilesPresenter(ResultViewModel<UsersController> vm)
    : ResultPresenter<UsersController, GetUserProfilesResponse, PagedResult<UserProfileDto>>(vm);

internal sealed class UpdateUserProfilePresenter(ResultViewModel<UsersController> vm)
    : ResultPresenter<UsersController, UpdateUserProfileResponse, UserProfileDto>(vm);

internal sealed class DisableUserProfilePresenter(ResultViewModel<UsersController> vm)
    : ResultPresenter<UsersController, DisableUserProfileResponse, UserProfileDto>(vm);
