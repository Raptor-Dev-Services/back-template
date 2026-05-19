using Authentication.Application.Dto;
using Authentication.Application.UseCases.RefreshToken.Responses;
using Authentication.Presentation;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;

namespace Authentication.Presentation.Presenters;

public sealed class RefreshTokenPresenter : INotificationHandler<RefreshTokenResponse>
{
    private readonly ResultViewModel<AuthController> _viewModel;

    public RefreshTokenPresenter(ResultViewModel<AuthController> viewModel) => _viewModel = viewModel;

    public Task Handle(RefreshTokenResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<TokenDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
