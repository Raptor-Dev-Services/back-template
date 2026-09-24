using Authentication.Application.Dto;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Presentation;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;

namespace Authentication.Presentation.Presenters;

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
