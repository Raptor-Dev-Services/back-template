using Application.Dto.Auth;
using Application.UseCases.Auth.RefreshToken.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Auth;

namespace WebApi.EndPoints.Auth.Presenters;

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
