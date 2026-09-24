using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Presentation;

namespace Users.Presentation.Presenters;

public sealed class DisableUserProfilePresenter : INotificationHandler<DisableUserProfileResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public DisableUserProfilePresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(DisableUserProfileResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess)
            _viewModel.OK(new { });

        return Task.CompletedTask;
    }
}
