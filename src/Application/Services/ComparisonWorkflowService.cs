using Application.Abstractions;
using Application.Models;
using Application.Services.Generation;

namespace Application.Services;

public sealed class ComparisonWorkflowService(
    IDatabaseMetadataReader metadataReader,
    IConnectionStringFactory connectionStringFactory,
    SchemaComparisonService comparisonService,
    ScriptGenerationStrategyResolver strategyResolver) : IComparisonWorkflowService
{
    public async Task<ComparisonWorkflowResult> CompareAsync(ComparisonWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OriginSchema) || string.IsNullOrWhiteSpace(request.DestinationSchema))
            throw new InvalidOperationException("Seleccione un esquema para ambas bases.");

        var originTask = metadataReader.GetTablesAsync(connectionStringFactory.Create(request.Origin), request.OriginSchema.Trim(), cancellationToken);
        var destinationTask = metadataReader.GetTablesAsync(connectionStringFactory.Create(request.Destination), request.DestinationSchema.Trim(), cancellationToken);
        await Task.WhenAll(originTask, destinationTask);
        cancellationToken.ThrowIfCancellationRequested();

        var originTables = originTask.Result
            .Where(table => request.TableFilter is null || !request.TableFilter.ShouldExclude(table.Name))
            .ToArray();
        var destinationTables = destinationTask.Result
            .Where(table => request.TableFilter is null || !request.TableFilter.ShouldExclude(table.Name))
            .ToArray();
        var comparison = comparisonService.Compare(originTables, destinationTables, request.CompareMissingTables, request.CompareMissingColumns);
        var generatedScript = strategyResolver
            .Get(request.GenerationMode)
            .Generate(comparison, request.SkipTablesWithDestinationOnlyColumns);

        return new ComparisonWorkflowResult(
            comparison,
            generatedScript,
            originTask.Result.Count - originTables.Length,
            destinationTask.Result.Count - destinationTables.Length);
    }
}
