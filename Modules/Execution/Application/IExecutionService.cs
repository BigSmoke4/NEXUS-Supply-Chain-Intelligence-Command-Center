namespace NEXUS.Modules.Execution.Application;

public interface IExecutionService
{
    Task<IReadOnlyList<Domain.Execution>> GetExecutionsAsync(int count = 25, CancellationToken cancellationToken = default);
    Task<Domain.Execution?> GetExecutionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Domain.Execution> ExecuteApprovedDecisionAsync(
        Guid decisionId,
        string executedBy = "Operations Manager",
        CancellationToken cancellationToken = default);
}
