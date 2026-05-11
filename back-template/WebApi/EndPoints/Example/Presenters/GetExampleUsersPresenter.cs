using Application.UseCases.Example.GetExampleUsers;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

/// <summary>
/// Presenter del caso de uso <c>GetExampleUsers</c>.
/// Usa <c>_viewModel.OK(success)</c> porque la respuesta exitosa contiene múltiples campos
/// (lista paginada) y no implementa <c>ISuccess&lt;T&gt;</c>.
/// </summary>
public sealed class GetExampleUsersPresenter : IPresenter<GetExampleUsersResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="GetExampleUsersPresenter"/>.
    /// </summary>
    /// <param name="viewModel">ViewModel compartido con el controller del mismo scope.</param>
    public GetExampleUsersPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    /// <summary>
    /// Maneja la notificación de respuesta y rellena el ViewModel.
    /// </summary>
    /// <param name="notification">Respuesta emitida por el handler.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea completada.</returns>
    public Task Handle(GetExampleUsersResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetExampleUsersSuccess success)
            _viewModel.OK(success);

        return Task.CompletedTask;
    }
}
