using Application.UseCases.Example.DisableExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

/// <summary>
/// Presenter del caso de uso <c>DisableExampleUser</c>.
/// </summary>
public sealed class DisableExampleUserPresenter : IPresenter<DisableExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="DisableExampleUserPresenter"/>.
    /// </summary>
    /// <param name="viewModel">ViewModel compartido con el controller del mismo scope.</param>
    public DisableExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    /// <summary>
    /// Maneja la notificación de respuesta y rellena el ViewModel.
    /// </summary>
    /// <param name="notification">Respuesta emitida por el handler.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea completada.</returns>
    public Task Handle(DisableExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess)
            _viewModel.OK(new { });

        return Task.CompletedTask;
    }
}
