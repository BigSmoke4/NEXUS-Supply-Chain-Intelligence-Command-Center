using NEXUS.Modules.Execution.Domain;

namespace NEXUS.Modules.Execution.Application;

public interface IExecutionService
{
    Task<IReadOnlyList<Execution>> GetExecutionsAsync(int count = 25, CancellationToken cancellationToken = default);
    Task<Execution?> GetExecutionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Execution> ExecuteApprovedDecisionAsync(
        Guid decisionId,
        string executedBy = "Operations Manager",
        CancellationToken cancellationToken = default);
}
