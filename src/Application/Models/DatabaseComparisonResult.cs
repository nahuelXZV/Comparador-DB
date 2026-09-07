using Domain.Models;

namespace Application.Models;

public sealed record MissingColumn(string Schema, string Table, ColumnDefinition Column);

public sealed record DatabaseComparisonResult(
    IReadOnlyList<TableDefinition> MissingTables,
    IReadOnlyList<MissingColumn> MissingColumns)
{
    public bool HasDifferences => MissingTables.Count > 0 || MissingColumns.Count > 0;
}
