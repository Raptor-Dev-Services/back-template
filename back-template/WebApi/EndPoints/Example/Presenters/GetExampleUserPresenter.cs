using Application.Dto.Example.User;
using Application.UseCases.Example.GetExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class GetExampleUserPresenter : IPresenter<GetExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public GetExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<ExampleUserDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
