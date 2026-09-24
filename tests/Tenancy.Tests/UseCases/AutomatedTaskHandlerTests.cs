using NSubstitute;
using Shared.Kernel.Audit;
using Shared.Kernel.BackgroundJobs;
using Shared.Kernel.Context;
using Shared.Kernel.Results;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow.Responses;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled.Responses;
using Xunit;

namespace Tenancy.Tests.UseCases;

// Las tareas son de toda la plataforma: tasks.manage no basta, hay que ser del tenant operador. Estas pruebas fijan
// esa puerta y que ninguna operacion denegada o fallida deje rastro en la bitacora ni toque el almacen.
public sealed class AutomatedTaskHandlerTests
{
    private const long OperatorTenant = 1;
    private const string Code = "auth.purge-expired-sessions";

    private readonly IAutomatedTaskRunner _runner = Substitute.For<IAutomatedTaskRunner>();
    private readonly IAutomatedTaskStore _store = Substitute.For<IAutomatedTaskStore>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static ICurrentUser UserOf(long? tenantId)
    {
        var user = Substitute.For<ICurrentUser>();
        user.TenantId.Returns(tenantId);
        user.UserId.Returns(Guid.NewGuid());
        return user;
    }

    private static BackgroundJobsOptions Options(long? operatorTenant = OperatorTenant) => new() { OperatorTenantId = operatorTenant };

    private RunAutomatedTaskNowHandler RunNow(ICurrentUser user, BackgroundJobsOptions? options = null) =>
        new(_runner, _store, options ?? Options(), user, _audit, _unitOfWork);

    private SetAutomatedTaskEnabledHandler SetEnabled(ICurrentUser user, BackgroundJobsOptions? options = null) =>
        new(_store, options ?? Options(), user, _audit, _unitOfWork);

    [Fact]
    public async Task RunNow_TenantQueNoEsElOperador_EsForbiddenYNoEjecuta()
    {
        var result = await RunNow(UserOf(2)).Handle(new RunAutomatedTaskNowRequest(Code), default);

        Assert.IsType<RunAutomatedTaskNowForbiddenFailure>(result);
        await _runner.DidNotReceiveWithAnyArgs().RunNowAsync(default!, default, default);
        _audit.DidNotReceiveWithAnyArgs().Append(default!, default!, default, default!);
    }

    [Fact]
    public async Task RunNow_SinTenantOperadorConfigurado_NadieOpera()
    {
        // Sin BackgroundJobs:OperatorTenantId la puerta queda cerrada para TODOS, incluido el tenant 1.
        var result = await RunNow(UserOf(OperatorTenant), Options(operatorTenant: null))
            .Handle(new RunAutomatedTaskNowRequest(Code), default);

        Assert.IsType<RunAutomatedTaskNowForbiddenFailure>(result);
    }

    [Fact]
    public async Task RunNow_TareaDesconocida_EsNotFoundSinBitacora()
    {
        _runner.RunNowAsync(Code, Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(RunNowOutcome.UnknownTask);

        var result = await RunNow(UserOf(OperatorTenant)).Handle(new RunAutomatedTaskNowRequest(Code), default);

        Assert.IsType<RunAutomatedTaskNowNotFoundFailure>(result);
        _audit.DidNotReceiveWithAnyArgs().Append(default!, default!, default, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RunNow_YaCorriendo_EsConflictSinBitacora()
    {
        _runner.RunNowAsync(Code, Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(RunNowOutcome.AlreadyRunning);

        var result = await RunNow(UserOf(OperatorTenant)).Handle(new RunAutomatedTaskNowRequest(Code), default);

        Assert.IsType<RunAutomatedTaskNowConflictFailure>(result);
        _audit.DidNotReceiveWithAnyArgs().Append(default!, default!, default, default!);
    }

    [Fact]
    public async Task RunNow_Operador_EjecutaAnotaYDevuelveLaUltimaCorrida()
    {
        var user = UserOf(OperatorTenant);
        var run = new AutomatedTaskRunDto(7, DateTime.UtcNow, DateTime.UtcNow, "Success", 3, 0, null,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, user.UserId);
        _runner.RunNowAsync(Code, user.UserId, Arg.Any<CancellationToken>()).Returns(RunNowOutcome.Ran);
        _store.GetHistoryAsync(Code, 1, 1, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AutomatedTaskRunDto>([run], 1, 1, 1));

        var result = await RunNow(user).Handle(new RunAutomatedTaskNowRequest(Code), default);

        var success = Assert.IsType<RunAutomatedTaskNowSuccess>(result);
        Assert.Equal(7, success.Data.Id);
        _audit.Received(1).Append("task.run_now", "AutomatedTask", Code, Arg.Any<string>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetEnabled_TenantQueNoEsElOperador_EsForbiddenYNoTocaElAlmacen()
    {
        var result = await SetEnabled(UserOf(2)).Handle(new SetAutomatedTaskEnabledRequest(Code, false), default);

        Assert.IsType<SetAutomatedTaskEnabledForbiddenFailure>(result);
        await _store.DidNotReceiveWithAnyArgs().SetEnabledAsync(default!, default, default);
    }

    [Fact]
    public async Task SetEnabled_TareaDesconocida_EsNotFoundSinBitacora()
    {
        _store.SetEnabledAsync(Code, false, Arg.Any<CancellationToken>()).Returns(false);

        var result = await SetEnabled(UserOf(OperatorTenant)).Handle(new SetAutomatedTaskEnabledRequest(Code, false), default);

        Assert.IsType<SetAutomatedTaskEnabledNotFoundFailure>(result);
        _audit.DidNotReceiveWithAnyArgs().Append(default!, default!, default, default!);
    }

    [Theory]
    [InlineData(false, "task.paused")]
    [InlineData(true, "task.resumed")]
    public async Task SetEnabled_Operador_AnotaLaAccionCorrecta(bool enable, string expectedAction)
    {
        _store.SetEnabledAsync(Code, enable, Arg.Any<CancellationToken>()).Returns(true);
        _store.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new AutomatedTaskStatusDto(Code, enable, 60, null, DateTime.UtcNow, null, false, null, null),
        ]);

        var result = await SetEnabled(UserOf(OperatorTenant)).Handle(new SetAutomatedTaskEnabledRequest(Code, enable), default);

        var success = Assert.IsType<SetAutomatedTaskEnabledSuccess>(result);
        Assert.Equal(enable, success.Data.IsEnabled);
        _audit.Received(1).Append(expectedAction, "AutomatedTask", Code, Arg.Any<string>());
    }
}
