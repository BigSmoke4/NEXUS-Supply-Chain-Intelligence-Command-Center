namespace NEXUS.Shared.Kernel;

public enum BackgroundJobState
{
    QUEUED,
    RUNNING,
    COMPLETED,
    FAILED,
    CANCELLED
}

public enum BackgroundJobType
{
    MonteCarloSimulation,
    Optimization,
    Forecasting,
    MlPrediction,
    LargeGraphAnalysis,
    ReportGeneration
}

public sealed record BackgroundJobStatusDto(
    Guid JobId,
    BackgroundJobType JobType,
    BackgroundJobState State,
    int ProgressPercent,
    string CurrentStage,
    Guid? ReferenceEntityId,
    string? ErrorMessage,
    DateTime EnqueuedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);

public interface IBackgroundJobService
{
    Task<Guid> EnqueueAsync(
        BackgroundJobType jobType,
        string initialStage,
        Func<IServiceProvider, Action<int, string>, CancellationToken, Task<Guid?>> workItem,
        CancellationToken cancellationToken = default);

    Task<BackgroundJobStatusDto?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackgroundJobStatusDto>> GetRecentJobsAsync(int count = 20, CancellationToken cancellationToken = default);

    Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
