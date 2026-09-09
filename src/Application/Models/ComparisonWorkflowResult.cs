namespace Application.Models;

public sealed record ComparisonWorkflowResult(
    DatabaseComparisonResult Comparison,
    ScriptGenerationResult GeneratedScript,
    int ExcludedOriginTables,
    int ExcludedDestinationTables);
