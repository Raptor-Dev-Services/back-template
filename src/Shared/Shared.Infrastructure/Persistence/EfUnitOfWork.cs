using System.Data;
using System.Globalization;
using Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Kernel.Context;

namespace Shared.Infrastructure.Persistence;

/// <summary><see cref="IUnitOfWork"/> sobre el DbContext de la peticion. Anidable: reusa una transaccion abierta.</summary>
public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
            return await work(cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// <see cref="ITenantScope"/>: fija el tenant en el accessor de Common (filtro de EF + interceptor de RLS) y,
/// si la conexion del DbContext YA esta abierta -una transaccion en curso-, actualiza el GUC en el acto: el
/// interceptor solo lo fija al abrir, y sin esto las escrituras siguientes irian con el tenant anterior.
/// </summary>
public sealed class TenantScope(AppDbContext db, ITenantContextAccessor tenantAccessor) : ITenantScope
{
    private readonly ITenantContextAccessor _accessor = tenantAccessor;

    public IDisposable Enter(long tenantId)
    {
        var previous = _accessor.Current;
        _accessor.Current = new TenantContext(tenantId.ToString(CultureInfo.InvariantCulture));
        SyncGuc();
        return new Restore(this, previous);
    }

    private void SyncGuc()
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            return;

        using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tenant";
        parameter.Value = _accessor.Current?.TenantId ?? string.Empty;
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
    }

    private sealed class Restore(TenantScope scope, TenantContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            scope._accessor.Current = previous;
            scope.SyncGuc();
        }
    }
}
