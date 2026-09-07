using System.Globalization;
using System.Text;
using Application.Models;
using Domain.Models;

namespace Application.Services.Generation;

/// <summary>
/// Genera cambios aditivos sin eliminar ni reconstruir tablas existentes.
/// </summary>
public sealed class SafeUpdateScriptGenerationStrategy : IScriptGenerationStrategy
{
    public ScriptGenerationMode Mode => ScriptGenerationMode.SafeUpdate;

    public ScriptGenerationResult Generate(DatabaseComparisonResult comparison)
    {
        var statements = new List<string>();
        var warnings = new List<string>();

        foreach (var table in comparison.MissingTables)
        {
            if (table.Columns.Count == 0)
            {
                warnings.Add($"La tabla [{table.Schema}].[{table.Name}] no tiene columnas y fue omitida.");
                continue;
            }

            statements.Add(BuildCreateTable(table));
        }

        foreach (var missingColumn in comparison.MissingColumns)
        {
            var statement = BuildAddColumn(missingColumn, warnings);
            if (statement is not null)
                statements.Add(statement);
        }

        return new ScriptGenerationResult(BuildTransaction(statements, warnings), warnings, statements.Count);
    }

    private static string? BuildAddColumn(MissingColumn missingColumn, ICollection<string> warnings)
    {
        var column = missingColumn.Column;
        var tableName = $"{Quote(missingColumn.Schema)}.{Quote(missingColumn.Table)}";

        if (column.IsIdentity)
        {
            warnings.Add($"[{missingColumn.Schema}].[{missingColumn.Table}].[{column.Name}] es IDENTITY. SQL Server asignará valores nuevos a las filas existentes; el orden de asignación no está garantizado.");
            return $"ALTER TABLE {tableName}\n    ADD {BuildColumnDefinition(column)};";
        }

        if (IsRowVersion(column))
        {
            warnings.Add($"[{missingColumn.Schema}].[{missingColumn.Table}].[{column.Name}] es ROWVERSION. SQL Server generará sus valores automáticamente.");
            return $"ALTER TABLE {tableName}\n    ADD {BuildColumnDefinition(column)};";
        }

        if (column.IsNullable)
        {
            var nullableDefinition = BuildColumnDefinition(column, includeDefault: column.DefaultExpression is not null);
            return $"ALTER TABLE {tableName}\n    ADD {nullableDefinition};";
        }

        var defaultExpression = column.DefaultExpression;
        if (string.IsNullOrWhiteSpace(defaultExpression))
        {
            defaultExpression = GetFallbackDefault(column);
            if (defaultExpression is null)
            {
                warnings.Add($"[{missingColumn.Schema}].[{missingColumn.Table}].[{column.Name}] es NOT NULL y no tiene una constante segura para su tipo [{column.DataType}]. Fue omitida.");
                return null;
            }

            warnings.Add($"[{missingColumn.Schema}].[{missingColumn.Table}].[{column.Name}] es NOT NULL sin DEFAULT de origen. Se usará {defaultExpression} para las filas existentes y futuras.");
        }

        return $"ALTER TABLE {tableName}\n    ADD {BuildColumnDefinition(column)} DEFAULT {defaultExpression} WITH VALUES;";
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

        if (warnings.Count > 0)
        {
            script.AppendLine("    -- Avisos para revisar antes de ejecutar:");
            foreach (var warning in warnings)
                script.AppendLine($"    -- {warning}");

            script.AppendLine();
        }

        script.AppendLine("    COMMIT TRANSACTION;");
        script.AppendLine("END TRY");
        script.AppendLine("BEGIN CATCH");
        script.AppendLine("    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;");
        script.AppendLine("    THROW;");
        script.AppendLine("END CATCH;");
        return script.ToString();
    }

    private static string BuildCreateTable(TableDefinition table)
    {
        var columns = table.Columns
            .OrderBy(column => column.Ordinal)
            .Select(column => BuildColumnDefinition(column, includeDefault: true))
            .ToArray();
        var script = new StringBuilder();
        script.AppendLine($"CREATE TABLE {Quote(table.Schema)}.{Quote(table.Name)} (");

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

    private static string BuildColumnDefinition(ColumnDefinition column, bool includeDefault = false)
    {
        var identity = column.IsIdentity
            ? $" IDENTITY({column.IdentitySeed?.ToString(CultureInfo.InvariantCulture) ?? "1"},{column.IdentityIncrement?.ToString(CultureInfo.InvariantCulture) ?? "1"})"
            : string.Empty;
        var nullability = column.IsNullable ? " NULL" : " NOT NULL";
        var defaultDefinition = includeDefault && !column.IsIdentity && !string.IsNullOrWhiteSpace(column.DefaultExpression)
            ? $" DEFAULT {column.DefaultExpression}"
            : string.Empty;
        return $"{Quote(column.Name)} {FormatDataType(column)}{identity}{nullability}{defaultDefinition}";
    }

    private static bool IsRowVersion(ColumnDefinition column) =>
        column.DataType.Equals("rowversion", StringComparison.OrdinalIgnoreCase) ||
        column.DataType.Equals("timestamp", StringComparison.OrdinalIgnoreCase);

    private static string? GetFallbackDefault(ColumnDefinition column)
    {
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

    private static string FormatLength(int? length) => length switch
    {
        -1 => "MAX",
        > 0 => length.Value.ToString(CultureInfo.InvariantCulture),
        _ => "1"
    };

    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
