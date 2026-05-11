using Application.UseCases.Example.DisableExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class DisableExampleUserPresenter : IPresenter<DisableExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public DisableExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(DisableExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);

        else if (notification is ISuccess)
            _viewModel.OK(new { });

        return Task.CompletedTask;
    }
}
