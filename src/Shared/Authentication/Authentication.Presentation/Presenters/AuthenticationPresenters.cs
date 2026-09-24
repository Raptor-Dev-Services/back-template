using Authentication.Application.Dto;
using Authentication.Application.UseCases.BootstrapTenant.Responses;
using Authentication.Application.UseCases.ChangePassword.Responses;
using Authentication.Application.UseCases.GetMyAccount.Responses;
using Authentication.Application.UseCases.GetRoles.Responses;
using Authentication.Application.UseCases.InviteUser.Responses;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Application.UseCases.Logout.Responses;
using Authentication.Application.UseCases.RefreshSession.Responses;
using Authentication.Application.UseCases.RequestPasswordReset.Responses;
using Authentication.Application.UseCases.ResetPassword.Responses;
using Authentication.Application.UseCases.SetUserLock.Responses;
using Authentication.Presentation.Controllers;
using Common.ViewModels;
using Shared.Web;

namespace Authentication.Presentation.Presenters;

// Un presenter por caso de uso, registrado a mano en ServiceCollectionEx. El mapeo comun vive en ResultPresenter.

internal sealed class LoginPresenter(ResultViewModel<AuthController> vm)
    : ResultPresenter<AuthController, LoginResponse, AuthTokensDto>(vm);

internal sealed class RefreshSessionPresenter(ResultViewModel<AuthController> vm)
    : ResultPresenter<AuthController, RefreshSessionResponse, AuthTokensDto>(vm);

internal sealed class LogoutPresenter(ResultViewModel<AuthController> vm)
    : ResultPresenter<AuthController, LogoutResponse, AcceptedDto>(vm);

internal sealed class RequestPasswordResetPresenter(ResultViewModel<AuthController> vm)
    : ResultPresenter<AuthController, RequestPasswordResetResponse, AcceptedDto>(vm);

internal sealed class ResetPasswordPresenter(ResultViewModel<AuthController> vm)
    : ResultPresenter<AuthController, ResetPasswordResponse, AcceptedDto>(vm);

internal sealed class ChangePasswordPresenter(ResultViewModel<AccountController> vm)
    : ResultPresenter<AccountController, ChangePasswordResponse, AcceptedDto>(vm);

internal sealed class GetMyAccountPresenter(ResultViewModel<AccountController> vm)
    : ResultPresenter<AccountController, GetMyAccountResponse, AccountDto>(vm);

internal sealed class BootstrapTenantPresenter(ResultViewModel<BootstrapController> vm)
    : ResultPresenter<BootstrapController, BootstrapTenantResponse, BootstrapResultDto>(vm);

internal sealed class InviteUserPresenter(ResultViewModel<AccountsController> vm)
    : ResultPresenter<AccountsController, InviteUserResponse, InvitedUserDto>(vm);

internal sealed class SetUserLockPresenter(ResultViewModel<AccountsController> vm)
    : ResultPresenter<AccountsController, SetUserLockResponse, AcceptedDto>(vm);

internal sealed class GetRolesPresenter(ResultViewModel<RolesController> vm)
    : ResultPresenter<RolesController, GetRolesResponse, IReadOnlyList<RoleDto>>(vm);
