using Application.UseCases.Example.UpdateExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class UpdateExampleUserPresenter : IPresenter<UpdateExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public UpdateExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(UpdateExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess)
            _viewModel.OK(new { });

        return Task.CompletedTask;
    }
}
