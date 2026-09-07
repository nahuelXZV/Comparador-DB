using Application.Models;
using Domain.Models;

namespace Application.Services;

public sealed class SchemaComparisonService
{
    public DatabaseComparisonResult Compare(
        IReadOnlyList<TableDefinition> originTables,
        IReadOnlyList<TableDefinition> destinationTables)
    {
        var destinationByName = destinationTables.ToDictionary(
            table => GetTableKey(table.Schema, table.Name),
            StringComparer.OrdinalIgnoreCase);

        var missingTables = new List<TableDefinition>();
        var missingColumns = new List<MissingColumn>();

        foreach (var originTable in originTables)
        {
            if (!destinationByName.TryGetValue(GetTableKey(originTable.Schema, originTable.Name), out var destinationTable))
            {
                missingTables.Add(originTable);
                continue;
            }

            var destinationColumns = destinationTable.Columns
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var originColumn in originTable.Columns)
            {
                if (!destinationColumns.Contains(originColumn.Name))
                    missingColumns.Add(new MissingColumn(originTable.Schema, originTable.Name, originColumn));
            }
        }

        return new DatabaseComparisonResult(missingTables, missingColumns);
    }

    private static string GetTableKey(string schema, string table) => $"{schema}.{table}";
}
