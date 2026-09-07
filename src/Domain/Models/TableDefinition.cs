namespace Domain.Models;

public sealed record TableDefinition(string Schema, string Name, IReadOnlyList<ColumnDefinition> Columns);
