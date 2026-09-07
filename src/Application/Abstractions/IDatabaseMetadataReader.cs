using Domain.Models;

namespace Application.Abstractions;

/// <summary>Lee únicamente metadatos estructurales de una base de datos.</summary>
public interface IDatabaseMetadataReader
{
    Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSchemasAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableDefinition>> GetTablesAsync(string connectionString, string schema, CancellationToken cancellationToken = default);
}
