using Application.Dto.Users;
using Application.UseCases.Users.CreateUser.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Users;

namespace WebApi.EndPoints.Users.Presenters;

public sealed class CreateUserPresenter : INotificationHandler<CreateUserResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public CreateUserPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(CreateUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<UserDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
