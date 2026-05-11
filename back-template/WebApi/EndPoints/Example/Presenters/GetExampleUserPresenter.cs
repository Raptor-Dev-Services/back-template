using Application.Dto.Example.User;
using Application.UseCases.Example.GetExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

/// <summary>
/// Presenter del caso de uso <c>GetExampleUser</c>.
/// Recibe la respuesta publicada por el <c>InteractorPipeline</c> y rellena el <see cref="ResultViewModel{T}"/>.
/// </summary>
public sealed class GetExampleUserPresenter : IPresenter<GetExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetExampleUserPresenter"/>.
    /// </summary>
    /// <param name="viewModel">ViewModel compartido con el controller del mismo scope.</param>
    public GetExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    /// <summary>
    /// Maneja la notificación de respuesta y rellena el ViewModel.
    /// </summary>
    /// <param name="notification">Respuesta emitida por el handler.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea completada.</returns>
    public Task Handle(GetExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<ExampleUserDto> success)
            _viewModel.Set(success);

        return Task.CompletedTask;
    }
}
