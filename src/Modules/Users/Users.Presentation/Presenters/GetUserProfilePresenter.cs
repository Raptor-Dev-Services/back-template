using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Contracts.Dtos;
using Users.Presentation;

namespace Users.Presentation.Presenters;

public sealed class GetUserProfilePresenter : INotificationHandler<GetUserProfileResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public GetUserProfilePresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(GetUserProfileResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<UserProfileDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
