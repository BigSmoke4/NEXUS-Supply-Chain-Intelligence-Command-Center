using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NEXUS.Shared.Kernel;

namespace NEXUS.Shared.Infrastructure;

public sealed record QueuedJobWorkItem(
    Guid JobId,
    BackgroundJobType JobType,
    Func<IServiceProvider, Action<int, string>, CancellationToken, Task<Guid?>> WorkItem,
    CancellationTokenSource CancellationSource);

public sealed class BackgroundJobService : IBackgroundJobService
{
    private readonly Channel<QueuedJobWorkItem> _channel = Channel.CreateUnbounded<QueuedJobWorkItem>();
    private readonly ConcurrentDictionary<Guid, BackgroundJobStatusDto> _statuses = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = new();
    private readonly INexusCacheService _cacheService;

    public BackgroundJobService(INexusCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    internal ChannelReader<QueuedJobWorkItem> Reader => _channel.Reader;

    public async Task<Guid> EnqueueAsync(
        BackgroundJobType jobType,
        string initialStage,
        Func<IServiceProvider, Action<int, string>, CancellationToken, Task<Guid?>> workItem,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var dto = new BackgroundJobStatusDto(
            jobId,
            jobType,
            BackgroundJobState.QUEUED,
            0,
            initialStage,
            null,
            null,
            DateTime.UtcNow,
            null,
            null);

        _statuses[jobId] = dto;
        _cancellations[jobId] = cts;
        await _cacheService.SetAsync(NexusCacheKeys.JobStatus(jobId), dto, TimeSpan.FromHours(2), cancellationToken);

        await _channel.Writer.WriteAsync(new QueuedJobWorkItem(jobId, jobType, workItem, cts), cancellationToken);
        return jobId;
    }

    public async Task<BackgroundJobStatusDto?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_statuses.TryGetValue(jobId, out var status))
        {
            return status;
        }

        return await _cacheService.GetAsync<BackgroundJobStatusDto>(NexusCacheKeys.JobStatus(jobId), cancellationToken);
    }

    public Task<IReadOnlyList<BackgroundJobStatusDto>> GetRecentJobsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var list = _statuses.Values
            .OrderByDescending(x => x.EnqueuedAtUtc)
            .Take(count)
            .ToList();
        return Task.FromResult<IReadOnlyList<BackgroundJobStatusDto>>(list);
    }

    public async Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_cancellations.TryGetValue(jobId, out var cts))
        {
            await cts.CancelAsync();
        }

        if (_statuses.TryGetValue(jobId, out var current))
        {
            var cancelled = current with
            {
                State = BackgroundJobState.CANCELLED,
                CurrentStage = "Cancelled by operator",
                CompletedAtUtc = DateTime.UtcNow
            };
            _statuses[jobId] = cancelled;
            await _cacheService.SetAsync(NexusCacheKeys.JobStatus(jobId), cancelled, TimeSpan.FromHours(2), cancellationToken);
            return true;
        }

        return false;
    }

    internal async Task UpdateStatusAsync(BackgroundJobStatusDto updated, CancellationToken cancellationToken = default)
    {
        _statuses[updated.JobId] = updated;
        await _cacheService.SetAsync(NexusCacheKeys.JobStatus(updated.JobId), updated, TimeSpan.FromHours(2), cancellationToken);
    }
}

public sealed class NexusBackgroundHostedService : BackgroundService
{
    private readonly BackgroundJobService _jobService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NexusBackgroundHostedService> _logger;

    public NexusBackgroundHostedService(
        BackgroundJobService jobService,
        IServiceScopeFactory scopeFactory,
        ILogger<NexusBackgroundHostedService> logger)
    {
        _jobService = jobService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _jobService.Reader.ReadAllAsync(stoppingToken))
        {
            var current = await _jobService.GetStatusAsync(item.JobId, stoppingToken);
            if (current is null || current.State == BackgroundJobState.CANCELLED)
            {
                continue;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, item.CancellationSource.Token);
            var running = current with
            {
                State = BackgroundJobState.RUNNING,
                ProgressPercent = 5,
                StartedAtUtc = DateTime.UtcNow
            };
            await _jobService.UpdateStatusAsync(running, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var refId = await item.WorkItem(
                    scope.ServiceProvider,
                    (pct, stage) =>
                    {
                        var progressDto = running with
                        {
                            ProgressPercent = Math.Clamp(pct, 0, 99),
                            CurrentStage = stage
                        };
                        _ = _jobService.UpdateStatusAsync(progressDto, CancellationToken.None);
                    },
                    linkedCts.Token);

                var completed = running with
                {
                    State = BackgroundJobState.COMPLETED,
                    ProgressPercent = 100,
                    CurrentStage = "Completed",
                    ReferenceEntityId = refId,
                    CompletedAtUtc = DateTime.UtcNow
                };
                await _jobService.UpdateStatusAsync(completed, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                var cancelled = running with
                {
                    State = BackgroundJobState.CANCELLED,
                    CurrentStage = "Cancelled",
                    CompletedAtUtc = DateTime.UtcNow
                };
                await _jobService.UpdateStatusAsync(cancelled, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job {JobId} ({JobType}) failed", item.JobId, item.JobType);
                var failed = running with
                {
                    State = BackgroundJobState.FAILED,
                    CurrentStage = "Execution failed",
                    ErrorMessage = ex.Message,
                    CompletedAtUtc = DateTime.UtcNow
                };
                await _jobService.UpdateStatusAsync(failed, CancellationToken.None);
            }
        }
    }
}
