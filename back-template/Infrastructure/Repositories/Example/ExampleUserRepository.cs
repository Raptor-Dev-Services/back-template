using Domain.Entities.Example;
using Domain.Repositories.Example;
using Infrastructure.Persistence.SQLDB.Main.Example;

namespace Infrastructure.Repositories.Example;

public sealed class ExampleUserRepository : IExampleUserRepository
{
    private readonly ExampleUsersSql _sql;

    public ExampleUserRepository(ExampleUsersSql sql) => _sql = sql;

    public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        _sql.GetByPublicIdAsync(publicId, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _sql.ExistsByEmailAsync(email, cancellationToken);

    public async Task<IReadOnlyCollection<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        (await _sql.GetPagedAsync(page, pageSize, cancellationToken).ConfigureAwait(false)).ToArray();

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default) =>
        _sql.GetCountAsync(cancellationToken);

    public Task<ExampleUser> InsertAsync(string fullName, string email, string department, string notes, CancellationToken cancellationToken = default) =>
        _sql.InsertAsync(fullName, email, department, notes, cancellationToken);

    public async Task<bool> UpdateAsync(Guid publicId, string fullName, string department, string notes, CancellationToken cancellationToken = default) =>
        await _sql.UpdateAsync(publicId, fullName, department, notes, cancellationToken).ConfigureAwait(false) > 0;

    public async Task<bool> DisableAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        await _sql.DisableAsync(publicId, cancellationToken).ConfigureAwait(false) > 0;
}
