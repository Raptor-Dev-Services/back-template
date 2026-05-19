using Authentication.Application.Dto;
using Authentication.Application.UseCases.Register.Responses;
using Authentication.Presentation;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;

namespace Authentication.Presentation.Presenters;

public sealed class RegisterPresenter : INotificationHandler<RegisterResponse>
{
    private readonly ResultViewModel<AuthController> _viewModel;

    public RegisterPresenter(ResultViewModel<AuthController> viewModel) => _viewModel = viewModel;

    public Task Handle(RegisterResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<TokenDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
