using System.Reflection;
using Api.IntegrationTests.Infrastructure;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests.Web;

/// <summary>
/// Los handlers y presenters se registran A MANO (sin escaneo de ensamblados, regla backend-architecture). El
/// precio es que olvidar uno no falla al compilar ni al arrancar: falla en la primera peticion a ese endpoint,
/// con un 500. Esta prueba lo convierte en un fallo de CI, sobre el contenedor REAL del Host.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompositionTests(PostgresFixture pg)
{
    private static readonly string[] ApplicationAssemblies =
    [
        "Authentication.Application",
        "Tenancy.Application",
        "Users.Application",
    ];

    private static IEnumerable<Type> Types() =>
        ApplicationAssemblies.Select(Assembly.Load).SelectMany(a => a.GetTypes());

    [Fact]
    public void Todo_handler_de_caso_de_uso_esta_registrado()
    {
        using var scope = pg.Api.Services.CreateScope();

        var missing = Types()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(i => (Handler: t, Service: i)))
            .Where(x => scope.ServiceProvider.GetService(x.Service)?.GetType() != x.Handler)
            .Select(x => $"{x.Handler.Name} ({x.Service.GetGenericArguments()[0].Name})")
            .ToList();

        Assert.True(missing.Count == 0,
            "Handlers que el mediador no encontraria (500 en la primera peticion): " + string.Join(", ", missing) +
            ". Registralos en el ServiceCollectionEx de su capa Application.");
    }

    [Fact]
    public void Todo_response_de_caso_de_uso_tiene_exactamente_un_presenter()
    {
        using var scope = pg.Api.Services.CreateScope();

        // Los Response de los casos de uso son los records abstractos que implementan IResponse.
        var responses = Types().Where(t => t.IsAbstract && typeof(IResponse).IsAssignableFrom(t)).ToList();
        Assert.NotEmpty(responses);

        var wrong = responses
            .Select(r => (Response: r, Count: scope.ServiceProvider
                .GetServices(typeof(INotificationHandler<>).MakeGenericType(r)).Count()))
            .Where(x => x.Count != 1)
            .Select(x => $"{x.Response.Name}: {x.Count} presenter(s)")
            .ToList();

        Assert.True(wrong.Count == 0,
            "Sin presenter el envelope sale vacio (200 con isSuccess:false y sin mensaje); con dos, el ultimo pisa al " +
            "primero: " + string.Join(", ", wrong) + ". Registra UNO en el ServiceCollectionEx de Presentation.");
    }
}
