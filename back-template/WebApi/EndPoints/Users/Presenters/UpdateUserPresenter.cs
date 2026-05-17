using Application.UseCases.Users.UpdateUser.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Users;

namespace WebApi.EndPoints.Users.Presenters;

public sealed class UpdateUserPresenter : INotificationHandler<UpdateUserResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public UpdateUserPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(UpdateUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is UpdateUserSuccess success)
            _viewModel.OK(success);

        return Task.CompletedTask;
    }
}
