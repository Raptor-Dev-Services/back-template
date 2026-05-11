using Application.Dto.Example.User;
using Application.UseCases.Example.InsertExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class InsertExampleUserPresenter : IPresenter<InsertExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public InsertExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(InsertExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<ExampleUserDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
