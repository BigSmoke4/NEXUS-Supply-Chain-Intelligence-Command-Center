using FluentValidation;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.Validation;

/// <summary>
/// Input for creating/running a scenario, validated before it ever reaches
/// the simulation engine (§43: validate every input). Bound from the
/// scenario builder form in ScenariosController.Run.
/// </summary>
public record RunScenarioRequest(Guid NodeId, DisruptionType Type, int DurationDays, double SeverityPercent, string? Name);

public class RunScenarioRequestValidator : AbstractValidator<RunScenarioRequest>
{
    public RunScenarioRequestValidator()
    {
        RuleFor(x => x.NodeId).NotEmpty().WithMessage("Select a node to disrupt.");

        RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, 365)
            .WithMessage("Duration must be between 1 and 365 days.");

        RuleFor(x => x.SeverityPercent)
            .InclusiveBetween(1, 100)
            .WithMessage("Severity must be between 1% and 100% capacity reduction.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Unrecognized disruption type.");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Scenario name must be 200 characters or fewer.");
    }
}
