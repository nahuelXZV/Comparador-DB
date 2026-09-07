using Application.Models;

namespace Application.Services.Generation;

/// <summary>
/// Reservada para la reconstrucción de tablas cuando se requiera conservar el orden de columnas.
/// </summary>
public sealed class RebuildTableScriptGenerationStrategy : IScriptGenerationStrategy
{
    public ScriptGenerationMode Mode => ScriptGenerationMode.RebuildTable;

    public ScriptGenerationResult Generate(DatabaseComparisonResult comparison) =>
        throw new NotImplementedException(
            "La estrategia de reconstrucción de tablas todavía no está implementada. Use Actualización segura.");
}
