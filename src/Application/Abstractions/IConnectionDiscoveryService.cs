using Application.Models;

namespace Application.Abstractions;

public interface IConnectionDiscoveryService
{
    Task<IReadOnlyList<string>> GetDatabasesAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSchemasAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken = default);
}
