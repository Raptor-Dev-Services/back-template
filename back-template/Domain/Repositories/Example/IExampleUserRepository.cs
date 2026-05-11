using Domain.Entities.Example;

namespace Domain.Repositories.Example;

public interface IExampleUserRepository
{
    Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ExampleUser>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<ExampleUser> InsertAsync(string fullName, string email, string department, string notes, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid publicId, string fullName, string department, string notes, CancellationToken cancellationToken = default);
    Task<bool> DisableAsync(Guid publicId, CancellationToken cancellationToken = default);
}
