using Domain.Entities.Example;
using Domain.Repositories.Example;
using Infrastructure.Persistence.SQLDB.Main.Example;

namespace Infrastructure.Repositories.Example;

/// <summary>
/// Implementación de <see cref="IExampleUserRepository"/> que delega a <see cref="ExampleUsersSql"/>.
/// </summary>
public sealed class ExampleUserRepository : IExampleUserRepository
{
    private readonly ExampleUsersSql _sql;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ExampleUserRepository"/>.
    /// </summary>
    /// <param name="sql">Objeto SQL que ejecuta los queries de la tabla.</param>
    public ExampleUserRepository(ExampleUsersSql sql) => _sql = sql;

    /// <inheritdoc/>
    public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        _sql.GetByPublicIdAsync(publicId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _sql.ExistsByEmailAsync(email, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        (await _sql.GetPagedAsync(page, pageSize, cancellationToken).ConfigureAwait(false)).ToArray();

    /// <inheritdoc/>
    public Task<int> GetCountAsync(CancellationToken cancellationToken = default) =>
        _sql.GetCountAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<ExampleUser> InsertAsync(string fullName, string email, string department, string notes, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(fullName, email, department, notes, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(Guid publicId, string fullName, string department, string notes, CancellationToken cancellationToken = default) =>
        await _sql.UpdateAsync(publicId, fullName, department, notes, cancellationToken).ConfigureAwait(false) > 0;

    /// <inheritdoc/>
    public async Task<bool> DisableAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        await _sql.DisableAsync(publicId, cancellationToken).ConfigureAwait(false) > 0;
}
