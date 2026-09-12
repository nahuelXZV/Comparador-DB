using System.Globalization;
using System.Text;
using Application.Models;
using Domain.Models;

namespace Application.Services.Generation;

/// <summary>
/// Reconstruye las tablas afectadas para igualar el orden de columnas de la base origen.
/// Solo conserva nombres, tipos, nulabilidad y valores por defecto; no replica IDENTITY,
/// índices, claves, triggers ni otros atributos.
/// </summary>
public sealed class RebuildTableScriptGenerationStrategy : IScriptGenerationStrategy
{
    public ScriptGenerationMode Mode => ScriptGenerationMode.RebuildTable;

    public ScriptGenerationResult Generate(DatabaseComparisonResult comparison, bool skipTablesWithDestinationOnlyColumns = true)
    {
        var statements = new List<string>();
        var warnings = new List<string>();

        foreach (var table in comparison.MissingTables)
            statements.Add(BuildCreateTable(table));

        foreach (var candidate in comparison.TablesToRebuild)
        {
            var statement = BuildRebuildTable(candidate, warnings, skipTablesWithDestinationOnlyColumns);
            if (statement is not null)
                statements.Add(statement);
        }

        return new ScriptGenerationResult(BuildTransaction(statements, warnings), warnings, statements.Count);
    }

    private static string? BuildRebuildTable(TableRebuildCandidate candidate, ICollection<string> warnings, bool skipTablesWithDestinationOnlyColumns)
    {
        var originTable = candidate.OriginTable;
        var destinationTable = candidate.DestinationTable;
        var originColumnNames = originTable.Columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var destinationOnlyColumns = destinationTable.Columns
            .Where(column => !originColumnNames.Contains(column.Name))
            .Select(column => column.Name)
            .ToArray();

        if (destinationOnlyColumns.Length > 0 && skipTablesWithDestinationOnlyColumns)
        {
            warnings.Add($"[{originTable.Schema}].[{originTable.Name}] tiene columnas solo en destino ({string.Join(", ", destinationOnlyColumns)}). Fue omitida para no eliminarlas.");
            return null;
        }

        if (destinationOnlyColumns.Length > 0)
        {
            warnings.Add($"[{originTable.Schema}].[{originTable.Name}] tiene columnas solo en destino ({string.Join(", ", destinationOnlyColumns)}). Se eliminarán durante la reconstrucción porque la protección está deshabilitada.");
        }

        var temporaryName = BuildGeneratedName(originTable.Name, "__Nueva");
        var backupName = BuildGeneratedName(originTable.Name, "__Anterior");
        var destinationColumnNames = destinationTable.Columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var insertColumns = new List<string>();
        var selectValues = new List<string>();

        foreach (var originColumn in originTable.Columns.OrderBy(column => column.Ordinal))
        {
            if (IsRowVersion(originColumn))
            {
                warnings.Add($"[{originTable.Schema}].[{originTable.Name}].[{originColumn.Name}] es ROWVERSION; SQL Server generará valores nuevos durante la reconstrucción.");
                continue;
            }

            insertColumns.Add(Quote(originColumn.Name));
            if (destinationColumnNames.Contains(originColumn.Name))
            {
                selectValues.Add(Quote(originColumn.Name));
                continue;
            }

            var valueForExistingRows = GetValueForNewColumn(originColumn);
            if (valueForExistingRows is null)
            {
                warnings.Add($"[{originTable.Schema}].[{originTable.Name}].[{originColumn.Name}] es NOT NULL y no tiene DEFAULT ni un valor automático seguro para [{originColumn.DataType}]. Fue omitida.");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(originColumn.DefaultExpression))
                selectValues.Add(originColumn.DefaultExpression);
            else
            {
                selectValues.Add(valueForExistingRows);
                warnings.Add($"[{originTable.Schema}].[{originTable.Name}].[{originColumn.Name}] no tiene DEFAULT. Se usará {valueForExistingRows} solo para copiar los registros existentes.");
            }
        }

        if (insertColumns.Count == 0)
        {
            warnings.Add($"[{originTable.Schema}].[{originTable.Name}] no tiene columnas copiables y fue omitida.");
            return null;
        }

        var qualifiedOriginalName = $"{Quote(originTable.Schema)}.{Quote(originTable.Name)}";
        var qualifiedTemporaryName = $"{Quote(originTable.Schema)}.{Quote(temporaryName)}";
        var qualifiedBackupName = $"{Quote(originTable.Schema)}.{Quote(backupName)}";
        var formattedInsertColumns = FormatSqlList(insertColumns);
        var formattedSelectValues = FormatSqlList(selectValues);

        return $"""
            {BuildCreateTable(originTable, temporaryName)}
            INSERT INTO {qualifiedTemporaryName} (
                {formattedInsertColumns}
            )
            SELECT {formattedSelectValues}
            FROM {qualifiedOriginalName};
            EXEC sys.sp_rename N'{EscapeSqlLiteral(qualifiedOriginalName)}', N'{EscapeSqlLiteral(backupName)}', N'OBJECT';
            EXEC sys.sp_rename N'{EscapeSqlLiteral(qualifiedTemporaryName)}', N'{EscapeSqlLiteral(originTable.Name)}', N'OBJECT';
            DROP TABLE {qualifiedBackupName};

            """.Trim();
    }

    private static string BuildTransaction(IReadOnlyList<string> statements, IReadOnlyList<string> warnings)
    {
        var script = new StringBuilder();
        script.AppendLine("SET XACT_ABORT ON;");
        script.AppendLine();
        script.AppendLine("BEGIN TRY");
        script.AppendLine("    BEGIN TRANSACTION;");
        script.AppendLine();

        if (statements.Count == 0)
            script.AppendLine("    -- No hay cambios para aplicar.");
        else
        {
            foreach (var statement in statements)
            {
                foreach (var line in statement.Split(Environment.NewLine))
                    script.AppendLine($"    {line}");

                script.AppendLine();
            }
        }

        script.AppendLine("    COMMIT TRANSACTION;");
        script.AppendLine("END TRY");
        script.AppendLine("BEGIN CATCH");
        script.AppendLine("    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;");
        script.AppendLine("    THROW;");
        script.AppendLine("END CATCH;");
        return script.ToString();
    }

    private static string BuildCreateTable(TableDefinition table, string? tableNameOverride = null)
    {
        var columns = table.Columns
            .OrderBy(column => column.Ordinal)
            .Select(BuildColumnDefinition)
            .ToArray();
        var tableName = tableNameOverride ?? table.Name;
        var script = new StringBuilder();
        script.AppendLine($"CREATE TABLE {Quote(table.Schema)}.{Quote(tableName)} (");

        for (var index = 0; index < columns.Length; index++)
        {
            script.Append("    ").Append(columns[index]);
            if (index < columns.Length - 1)
                script.Append(',');

            script.AppendLine();
        }

        script.Append(");");
        return script.ToString();
    }

    private static string BuildColumnDefinition(ColumnDefinition column)
    {
        var nullability = column.IsNullable ? " NULL" : " NOT NULL";
        var defaultDefinition = !string.IsNullOrWhiteSpace(column.DefaultExpression)
            ? $" DEFAULT {column.DefaultExpression}"
            : string.Empty;
        return $"{Quote(column.Name)} {FormatDataType(column)}{nullability}{defaultDefinition}";
    }

    private static string? GetValueForNewColumn(ColumnDefinition column)
    {
        if (column.IsNullable)
            return "NULL";

        return column.DataType.ToUpperInvariant() switch
        {
            "BIT" or "TINYINT" or "SMALLINT" or "INT" or "BIGINT" or "DECIMAL" or "NUMERIC" or "FLOAT" or "REAL" or "MONEY" or "SMALLMONEY" or "SQL_VARIANT" => "0",
            "CHAR" or "VARCHAR" => "''",
            "NCHAR" or "NVARCHAR" => "N''",
            "BINARY" or "VARBINARY" => "0x",
            "DATE" or "DATETIME" or "SMALLDATETIME" or "DATETIME2" => "'19000101'",
            "DATETIMEOFFSET" => "'1900-01-01T00:00:00+00:00'",
            "TIME" => "'00:00:00'",
            "UNIQUEIDENTIFIER" => "'00000000-0000-0000-0000-000000000000'",
            "XML" => "CONVERT(xml, N'<root />')",
            _ => null
        };
    }

    private static bool IsRowVersion(ColumnDefinition column) => column.DataType.Equals("rowversion", StringComparison.OrdinalIgnoreCase) || column.DataType.Equals("timestamp", StringComparison.OrdinalIgnoreCase);

    private static string FormatDataType(ColumnDefinition column)
    {
        var type = column.DataType.ToUpperInvariant();
        return type switch
        {
            "VARCHAR" or "CHAR" or "VARBINARY" or "BINARY" => $"{type}({FormatLength(column.MaxLength)})",
            "NVARCHAR" or "NCHAR" => $"{type}({FormatLength(column.MaxLength is null or -1 ? column.MaxLength : column.MaxLength / 2)})",
            "DECIMAL" or "NUMERIC" => $"{type}({column.Precision ?? 18},{column.Scale ?? 0})",
            "DATETIME2" or "DATETIMEOFFSET" or "TIME" => $"{type}({column.Scale ?? 7})",
            "FLOAT" when column.Precision is not null => $"FLOAT({column.Precision})",
            _ => type
        };
    }

    private static string BuildGeneratedName(string sourceName, string suffix)
    {
        var maximumSourceLength = 128 - suffix.Length;
        var baseName = sourceName.Length <= maximumSourceLength
            ? sourceName
            : sourceName[..maximumSourceLength];
        return $"{baseName}{suffix}";
    }

    private static string FormatSqlList(IReadOnlyList<string> values)
    {
        const int indentationLength = 4;
        const int maximumLineLength = 120;
        var indentation = new string(' ', indentationLength);
        var lines = new List<string>();
        var currentLine = new StringBuilder();

        for (var index = 0; index < values.Count; index++)
        {
            var token = values[index] + (index < values.Count - 1 ? "," : string.Empty);
            if (currentLine.Length > 0 && indentationLength + currentLine.Length + token.Length > maximumLineLength)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
            }

            currentLine.Append(token);
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine.ToString());

        return string.Join(Environment.NewLine + indentation, lines);
    }

    private static string FormatLength(int? length) => length switch
    {
        -1 => "MAX",
        > 0 => length.Value.ToString(CultureInfo.InvariantCulture),
        _ => "1"
    };

    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
