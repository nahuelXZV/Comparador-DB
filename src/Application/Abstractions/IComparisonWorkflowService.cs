using Application.Models;

namespace Application.Abstractions;

public interface IComparisonWorkflowService
{
    Task<ComparisonWorkflowResult> CompareAsync(ComparisonWorkflowRequest request, CancellationToken cancellationToken = default);
}
