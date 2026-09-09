namespace Application.Models;

public sealed record ComparisonWorkflowRequest(
    DatabaseConnectionSettings Origin,
    DatabaseConnectionSettings Destination,
    string OriginSchema,
    string DestinationSchema,
    bool CompareMissingTables,
    bool CompareMissingColumns,
    TableNameFilter? TableFilter,
    ScriptGenerationMode GenerationMode,
    bool SkipTablesWithDestinationOnlyColumns);
