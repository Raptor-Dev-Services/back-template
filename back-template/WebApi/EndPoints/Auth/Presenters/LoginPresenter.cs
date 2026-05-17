using Application.Dto.Auth;
using Application.UseCases.Auth.Login.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Auth;

namespace WebApi.EndPoints.Auth.Presenters;

public sealed class LoginPresenter : INotificationHandler<LoginResponse>
{
    private readonly ResultViewModel<AuthController> _viewModel;

    public LoginPresenter(ResultViewModel<AuthController> viewModel) => _viewModel = viewModel;

    public Task Handle(LoginResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<TokenDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
