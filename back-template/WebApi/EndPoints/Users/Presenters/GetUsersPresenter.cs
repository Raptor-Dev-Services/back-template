using Application.UseCases.Users.GetUsers.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Users;

namespace WebApi.EndPoints.Users.Presenters;

public sealed class GetUsersPresenter : INotificationHandler<GetUsersResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public GetUsersPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(GetUsersResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetUsersSuccess success)
            _viewModel.OK(success);

        return Task.CompletedTask;
    }
}
