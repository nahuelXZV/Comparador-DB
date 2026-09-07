using Application.Models;

namespace Application.Services.Generation;

public sealed class ScriptGenerationStrategyResolver(IEnumerable<IScriptGenerationStrategy> strategies)
{
    private readonly IReadOnlyDictionary<ScriptGenerationMode, IScriptGenerationStrategy> _strategies = strategies
        .ToDictionary(strategy => strategy.Mode);

    public IScriptGenerationStrategy Get(ScriptGenerationMode mode) =>
        _strategies.TryGetValue(mode, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"No existe una estrategia registrada para {mode}.");
}
