namespace Application.Models;

public sealed record ScriptGenerationResult(
    string Script,
    IReadOnlyList<string> Warnings,
    int GeneratedStatements);
