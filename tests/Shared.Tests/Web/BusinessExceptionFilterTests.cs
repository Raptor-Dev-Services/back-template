using Common.Results;
using Common.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Errors;
using Shared.Kernel.Results;
using Shared.Web.Errors;
using Xunit;

namespace Shared.Tests.Web;

public sealed class BusinessExceptionFilterTests
{
    public static TheoryData<Exception, int> BusinessExceptions => new()
    {
        { new BadRequestException("m"), StatusCodes.Status400BadRequest },
        { new UnauthorizedException("m"), StatusCodes.Status401Unauthorized },
        { new ForbiddenException("m"), StatusCodes.Status403Forbidden },
        { new NotFoundException("m"), StatusCodes.Status404NotFound },
        { new ConflictException("m"), StatusCodes.Status409Conflict },
        { new ValidationException("m"), StatusCodes.Status400BadRequest },
        { new Common.Exceptions.BusinessRuleException("m"), StatusCodes.Status400BadRequest },
    };

    [Theory]
    [MemberData(nameof(BusinessExceptions))]
    public void Excepcion_de_negocio_responde_su_status_y_su_mensaje(Exception exception, int expectedStatus)
    {
        var context = ContextFor(exception);

        new BusinessExceptionFilter(NullLogger<BusinessExceptionFilter>.Instance).OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
        var envelope = Assert.IsType<ResultViewModel<object>>(result.Value);
        Assert.False(envelope.IsSuccess);
        Assert.Equal("m", envelope.Message);
        Assert.Null(envelope.Data);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void Excepcion_inesperada_responde_500_sin_fugar_el_mensaje_interno()
    {
        // Lo que devolvia antes cada controller: el mensaje de la excepcion mas interna.
        var inner = new InvalidOperationException("23505: duplicate key value violates unique constraint \"UX_UserCredential_Email\"");
        var context = ContextFor(new Exception("wrapper", inner));

        new BusinessExceptionFilter(NullLogger<BusinessExceptionFilter>.Instance).OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var envelope = Assert.IsType<ResultViewModel<object>>(result.Value);
        Assert.Equal("Ha ocurrido un error inesperado.", envelope.Message);
        Assert.DoesNotContain("23505", envelope.Message);
        Assert.DoesNotContain("wrapper", envelope.Message);
    }

    [Fact]
    public void Cancelacion_del_cliente_responde_499_y_no_es_error()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = ContextFor(new OperationCanceledException(), requestAborted: cts.Token);

        new BusinessExceptionFilter(NullLogger<BusinessExceptionFilter>.Instance).OnException(context);

        var result = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal(BusinessExceptionFilter.StatusClientClosedRequest, result.StatusCode);
    }

    [Fact]
    public void Cancelacion_que_no_es_del_cliente_sigue_siendo_500()
    {
        // Un timeout interno tambien lanza OperationCanceledException, pero el request sigue vivo: es nuestro.
        var context = ContextFor(new OperationCanceledException());

        new BusinessExceptionFilter(NullLogger<BusinessExceptionFilter>.Instance).OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public void Respuesta_ya_empezada_no_intenta_escribir_el_envelope()
    {
        var context = ContextFor(new NotFoundException("m"), responseStarted: true);

        new BusinessExceptionFilter(NullLogger<BusinessExceptionFilter>.Instance).OnException(context);

        Assert.Null(context.Result);
        Assert.True(context.ExceptionHandled);
    }

    private sealed record Unmarked(string Message) : IFailure;
    private sealed record NotFound(string Message) : INotFoundFailure;
    private sealed record Conflict(string Message) : IConflictFailure;
    private sealed record Invalid(string Message) : IValidationFailure;
    private sealed record Unauthorized(string Message) : IUnauthorizedFailure;
    private sealed record Forbidden(string Message) : IForbiddenFailure;
    private sealed record BadRequest(string Message) : IBadRequestFailure;

    [Fact]
    public void Los_fallos_tipados_responden_el_mismo_status_que_su_excepcion()
    {
        Assert.Equal(400, FailureStatusCodes.For(new BadRequest("")));
        Assert.Equal(401, FailureStatusCodes.For(new Unauthorized("")));
        Assert.Equal(403, FailureStatusCodes.For(new Forbidden("")));
        Assert.Equal(404, FailureStatusCodes.For(new NotFound("")));
        Assert.Equal(409, FailureStatusCodes.For(new Conflict("")));
        Assert.Equal(400, FailureStatusCodes.For(new Invalid("")));
        Assert.Equal(400, FailureStatusCodes.For(new Unmarked("")));
    }

    private static ExceptionContext ContextFor(
        Exception exception, CancellationToken requestAborted = default, bool responseStarted = false)
    {
        var http = new DefaultHttpContext { RequestAborted = requestAborted };
        if (responseStarted)
            http.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(action, []) { Exception = exception };
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
