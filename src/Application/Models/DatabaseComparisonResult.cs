using Domain.Models;

namespace Application.Models;

public sealed record MissingColumn(string Schema, string Table, ColumnDefinition Column);

public sealed record TableRebuildCandidate(
    TableDefinition OriginTable,
    TableDefinition DestinationTable,
    IReadOnlyList<ColumnDefinition> MissingColumns);

public sealed record DatabaseComparisonResult(
    IReadOnlyList<TableDefinition> MissingTables,
    IReadOnlyList<MissingColumn> MissingColumns,
    IReadOnlyList<TableRebuildCandidate> TablesToRebuild)
{
    public bool HasDifferences => MissingTables.Count > 0 || MissingColumns.Count > 0;
}
