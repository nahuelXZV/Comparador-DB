using Application.Models;

namespace Application.Abstractions;

public interface IConnectionManagementService
{
    Task<IReadOnlyList<SavedConnectionProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SavedConnectionProfileDetails?> GetDetailsAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<SavedConnectionProfile> SaveAsync(SaveConnectionProfileCommand command, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
