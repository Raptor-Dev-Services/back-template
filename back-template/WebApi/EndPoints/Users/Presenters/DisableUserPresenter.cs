using Application.UseCases.Users.DisableUser.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Users;

namespace WebApi.EndPoints.Users.Presenters;

public sealed class DisableUserPresenter : INotificationHandler<DisableUserResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public DisableUserPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(DisableUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is DisableUserSuccess success)
            _viewModel.OK(success);

        return Task.CompletedTask;
    }
}
