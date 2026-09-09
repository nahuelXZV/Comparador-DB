using Application.Abstractions;
using Application.Models;

namespace Application.Services;

public sealed class ConnectionDiscoveryService(IDatabaseMetadataReader metadataReader, IConnectionStringFactory connectionStringFactory) : IConnectionDiscoveryService
{
    public Task<IReadOnlyList<string>> GetDatabasesAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken = default) =>
        metadataReader.GetDatabasesAsync(connectionStringFactory.Create(settings), cancellationToken);

    public Task<IReadOnlyList<string>> GetSchemasAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken = default) =>
        metadataReader.GetSchemasAsync(connectionStringFactory.Create(settings), cancellationToken);
}
