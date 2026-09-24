using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Presentation;

namespace Users.Presentation.Presenters;

public sealed class UpdateUserProfilePresenter : INotificationHandler<UpdateUserProfileResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public UpdateUserProfilePresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(UpdateUserProfileResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess)
            _viewModel.OK(new { });

        return Task.CompletedTask;
    }
}
