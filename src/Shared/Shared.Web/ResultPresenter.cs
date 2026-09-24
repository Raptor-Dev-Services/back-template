using Common.Messaging;
using Common.Results;
using Common.ViewModels;

namespace Shared.Web;

/// <summary>
/// Base de los presenters (<c>INotificationHandler&lt;TResponse&gt;</c>): vuelca el <c>Response</c> de un caso de uso
/// en el <see cref="ResultViewModel{T}"/> del controller. Cada caso de uso sigue teniendo SU presenter (una
/// clase sellada que hereda de aqui y se registra a mano), pero el mapeo comun no se copia en cada uno.
///
/// <para>Un response que no es ni fallo ni exito lanza: un presenter que no sabe que hacer y deja el envelope
/// vacio responderia 200 con <c>isSuccess: false</c> y sin mensaje, que es el peor error posible de depurar.</para>
///
/// Para dar forma propia a los datos (ocultar campos, aplanar), sobreescribe <see cref="Shape"/>.
/// </summary>
public abstract class ResultPresenter<TController, TResponse, TData>(ResultViewModel<TController> viewModel)
    : INotificationHandler<TResponse>
    where TResponse : IResponse
{
    public Task Handle(TResponse notification, CancellationToken cancellationToken)
    {
        switch (notification)
        {
            case IFailure failure:
                viewModel.Fail(failure.Message);
                break;
            case ISuccess<TData> success:
                viewModel.Set(success, Shape);
                break;
            default:
                throw new InvalidOperationException(
                    $"{GetType().Name} no sabe presentar {notification.GetType().Name}: no es IFailure ni ISuccess<{typeof(TData).Name}>.");
        }

        return Task.CompletedTask;
    }

    protected virtual object Shape(TData data) => data!;
}
