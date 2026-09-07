using Application.Abstractions;
using Domain.Models;
using Microsoft.Data.SqlClient;

namespace Infrastructure;

public sealed class DatabaseMetadataReader : IDatabaseMetadataReader
{
    public async Task<IReadOnlyList<string>> GetDatabasesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        const string sql = """
            SELECT [name]
            FROM sys.databases
            WHERE [state] = 0
            ORDER BY [name];
            """;

        return await GetSingleColumnAsync(builder.ConnectionString, sql, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetSchemasAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT s.[name]
            FROM sys.schemas AS s
            INNER JOIN sys.tables AS t ON t.[schema_id] = s.[schema_id]
            WHERE t.[is_ms_shipped] = 0
            ORDER BY s.[name];
            """;

        return await GetSingleColumnAsync(connectionString, sql, cancellationToken);
    }

    public async Task<IReadOnlyList<TableDefinition>> GetTablesAsync(
        string connectionString,
        string schema,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                t.[object_id],
                s.[name] AS [SchemaName],
                t.[name] AS [TableName],
                c.[name] AS [ColumnName],
                c.[column_id],
                ty.[name] AS [DataType],
                c.[max_length],
                c.[precision],
                c.[scale],
                c.[is_nullable],
                CASE WHEN ic.[object_id] IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS [IsIdentity],
                CONVERT(decimal(38, 0), ic.[seed_value]) AS [IdentitySeed],
                CONVERT(decimal(38, 0), ic.[increment_value]) AS [IdentityIncrement],
                dc.[definition] AS [DefaultExpression]
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
            INNER JOIN sys.columns AS c ON c.[object_id] = t.[object_id]
            INNER JOIN sys.types AS ty ON ty.[user_type_id] = c.[user_type_id]
            LEFT JOIN sys.identity_columns AS ic
                ON ic.[object_id] = c.[object_id] AND ic.[column_id] = c.[column_id]
            LEFT JOIN sys.default_constraints AS dc
                ON dc.[object_id] = c.[default_object_id]
            WHERE t.[is_ms_shipped] = 0
              AND s.[name] = @schema
            ORDER BY s.[name], t.[name], c.[column_id];
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@schema", System.Data.SqlDbType.NVarChar, 128) { Value = schema });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new Dictionary<int, MutableTable>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var objectId = reader.GetInt32(0);
            if (!tables.TryGetValue(objectId, out var table))
            {
                table = new MutableTable(reader.GetString(1), reader.GetString(2));
                tables.Add(objectId, table);
            }

            table.Columns.Add(new ColumnDefinition(
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetInt16(6),
                reader.GetByte(7),
                reader.GetByte(8),
                reader.GetBoolean(9),
                reader.GetBoolean(10),
                reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                reader.IsDBNull(12) ? null : reader.GetDecimal(12),
                reader.IsDBNull(13) ? null : reader.GetString(13)));
        }

        return tables.Values
            .Select(table => new TableDefinition(table.Schema, table.Name, table.Columns))
            .ToArray();
    }

    private static async Task<IReadOnlyList<string>> GetSingleColumnAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken)
    {
        var values = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            values.Add(reader.GetString(0));

        return values;
    }

    private sealed class MutableTable(string schema, string name)
    {
        public string Schema { get; } = schema;
        public string Name { get; } = name;
        public List<ColumnDefinition> Columns { get; } = [];
    }
}
