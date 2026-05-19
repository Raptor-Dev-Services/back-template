using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using Users.Application.UseCases.GetUserProfiles.Responses;
using Users.Presentation;

namespace Users.Presentation.Presenters;

public sealed class GetUserProfilesPresenter : INotificationHandler<GetUserProfilesResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public GetUserProfilesPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(GetUserProfilesResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetUserProfilesSuccess success)
            _viewModel.OK(success);

        return Task.CompletedTask;
    }
}
