using Application.Models;

namespace Application.Services.Generation;

public interface IScriptGenerationStrategy
{
    ScriptGenerationMode Mode { get; }

    ScriptGenerationResult Generate(DatabaseComparisonResult comparison);
}
