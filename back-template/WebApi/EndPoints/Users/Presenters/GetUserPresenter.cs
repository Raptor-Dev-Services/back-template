using Application.Dto.Users;
using Application.UseCases.Users.GetUser.Responses;
using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using WebApi.EndPoints.Users;

namespace WebApi.EndPoints.Users.Presenters;

public sealed class GetUserPresenter : INotificationHandler<GetUserResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;

    public GetUserPresenter(ResultViewModel<UsersController> viewModel) => _viewModel = viewModel;

    public Task Handle(GetUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<UserDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
