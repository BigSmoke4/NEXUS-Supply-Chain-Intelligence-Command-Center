using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.DecisionEngine.Application;
using NEXUS.Modules.Execution.Application;
using NEXUS.Modules.Execution.Domain;
using NEXUS.Shared.Data;
using NEXUS.Shared.Infrastructure;

namespace NEXUS.Modules.Execution.Infrastructure;

public sealed class ExecutionService : IExecutionService
{
    private readonly NexusDbContext _db;
    private readonly INexusCacheService _cache;
    private readonly IAuditService _audit;

    public ExecutionService(
        NexusDbContext db,
        INexusCacheService cache,
        IAuditService audit)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IReadOnlyList<Domain.Execution>> GetExecutionsAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        return await _db.Executions
            .AsNoTracking()
            .Include(e => e.Results)
            .OrderByDescending(e => e.InitiatedAtUtc)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<Domain.Execution?> GetExecutionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Executions
            .AsNoTracking()
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Domain.Execution> ExecuteApprovedDecisionAsync(
        Guid decisionId,
        string executedBy = "Operations Manager",
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .Include(d => d.Options)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken)
            ?? throw new InvalidOperationException("Decision not found.");

        if (decision.RequiresHumanApproval && decision.Status != "APPROVED" && decision.Status != "EXECUTED")
        {
            throw new InvalidOperationException($"Decision {decision.DecisionCode} requires human manager approval before execution (current status: {decision.Status}).");
        }

        decision.Status = "EXECUTED";
        decision.UpdatedAtUtc = DateTime.UtcNow;

        var timeline = new List<ReplayEventDto>();
        try
        {
            timeline = JsonSerializer.Deserialize<List<ReplayEventDto>>(decision.ReplayTimelineJson) ?? new List<ReplayEventDto>();
        }
        catch
        {
            // ignore
        }

        if (!timeline.Any(e => e.Event.Contains("Execution initiated", StringComparison.OrdinalIgnoreCase)))
        {
            timeline.Add(new ReplayEventDto("09:20", "Execution initiated", $"Dispatched Strategy {decision.RecommendedStrategyCode} work orders to Factory B (Dresden) and Supplier C (Nordic Silicon)."));
        }
        decision.ReplayTimelineJson = JsonSerializer.Serialize(timeline);

        var count = await _db.Executions.CountAsync(cancellationToken) + 1;
        var execution = new Domain.Execution
        {
            Id = Guid.NewGuid(),
            OrganizationId = NexusSeedData.DefaultOrganizationId,
            ExecutionCode = $"EXE-{count:D3}-{DateTime.UtcNow:HHmmss}",
            DecisionId = decision.Id,
            DecisionCode = decision.DecisionCode,
            SelectedStrategyCode = decision.RecommendedStrategyCode,
            ActionSummary = decision.WhatRecommendation,
            Status = "COMPLETED",
            ProgressPct = 100,
            ApprovedByUser = executedBy,
            InitiatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            ReplayEventsJson = decision.ReplayTimelineJson,
            Results = new List<ExecutionResult>
            {
                new()
                {
                    OrganizationId = NexusSeedData.DefaultOrganizationId,
                    TargetAssetCode = "FAC-002",
                    TargetAssetName = "FACTORY B — Dresden Autonomous Manufacturing Plant",
                    OperationPerformed = decision.WhatRecommendation,
                    CapacityShiftedUnitsPerDay = 4_200m,
                    CostIncurredUsd = Math.Round(decision.AdditionalCostUsd * 0.63m, 0),
                    PostExecutionUtilizationPct = 89.5m,
                    ServiceLevelAchievedPct = decision.ProjectedServiceLevelPct,
                    IsSuccessful = true
                },
                new()
                {
                    OrganizationId = NexusSeedData.DefaultOrganizationId,
                    TargetAssetCode = "SUP-003",
                    TargetAssetName = "Supplier C — Nordic Silicon Dynamics AB",
                    OperationPerformed = "Activated dual-source supply allocation (4,500 units/day at 97% reliability)",
                    CapacityShiftedUnitsPerDay = 4_500m,
                    CostIncurredUsd = Math.Round(decision.AdditionalCostUsd * 0.37m, 0),
                    PostExecutionUtilizationPct = 91.0m,
                    ServiceLevelAchievedPct = decision.ProjectedServiceLevelPct,
                    IsSuccessful = true
                }
            }
        };

        _db.Executions.Add(execution);
        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateEnterpriseStateAsync(cancellationToken);

        await _audit.RecordAsync(
            executedBy,
            "OperationsManager",
            "Execution",
            "DECISION_PLAN_EXECUTED",
            "Execution",
            execution.ExecutionCode,
            $"Executed approved decision {decision.DecisionCode} ({decision.RecommendedStrategyCode}).");

        return execution;
    }
}
