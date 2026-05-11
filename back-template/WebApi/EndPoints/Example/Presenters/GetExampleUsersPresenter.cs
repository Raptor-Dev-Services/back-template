using Application.UseCases.Example.GetExampleUsers;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class GetExampleUsersPresenter : IPresenter<GetExampleUsersResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public GetExampleUsersPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetExampleUsersResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetExampleUsersSuccess success)
            _viewModel.OK(success);

        return Task.CompletedTask;
    }
}
