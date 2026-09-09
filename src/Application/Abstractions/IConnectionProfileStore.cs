using Application.Models;

namespace Application.Abstractions;

public interface IConnectionProfileStore
{
    Task<IReadOnlyList<SavedConnectionProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SavedConnectionProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
