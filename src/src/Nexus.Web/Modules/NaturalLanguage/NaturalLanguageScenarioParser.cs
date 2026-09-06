using System.Text.RegularExpressions;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.NaturalLanguage;

public record ParsedScenario(Guid NodeId, string NodeName, DisruptionType Type, int DurationDays, double SeverityPercent);

public record ScenarioParseResult(ParsedScenario? Scenario, List<string> ClarificationsNeeded)
{
    public bool Succeeded => Scenario is not null;
}

/// <summary>
/// Extracts structured scenario parameters from free text like "Simulate
/// Supplier Alpha shutting down for 30 days" (§33), using deterministic
/// pattern matching against known node names and a fixed keyword map - never
/// an LLM guessing numbers. Per §33's own rule ("never silently assume
/// critical information"), any field it cannot extract with confidence is
/// reported as a clarification the caller must ask for, not defaulted.
/// </summary>
public interface INaturalLanguageScenarioParser
{
    ScenarioParseResult Parse(string text, IReadOnlyList<(Guid Id, string Name, NodeType Type)> candidateNodes);
}

public class NaturalLanguageScenarioParser : INaturalLanguageScenarioParser
{
    private static readonly Regex DurationPattern = new(@"(\d+)\s*(day|days|week|weeks|month|months)", RegexOptions.IgnoreCase);
    private static readonly Regex SeverityPattern = new(@"(\d{1,3})\s*%", RegexOptions.IgnoreCase);

    private static readonly Regex ShutdownPattern = new(@"\bshut(s|ting)?\s*down\b|\bcloses\b|\bclosure\b", RegexOptions.IgnoreCase);

    private static readonly (string[] Keywords, DisruptionType Type)[] TypeKeywords =
    {
        (new[] { "capacity reduction", "loses capacity", "reduced capacity", "% capacity" }, DisruptionType.FactoryCapacityReduction),
        (new[] { "port closure", "port closes" }, DisruptionType.PortClosure),
        (new[] { "transportation failure", "shipping route", "route unavailable", "route becomes unavailable" }, DisruptionType.TransportationFailure),
        (new[] { "demand increase", "demand spike", "demand rises" }, DisruptionType.DemandSpike),
        (new[] { "demand collapse", "demand drops", "demand falls" }, DisruptionType.DemandCollapse),
        (new[] { "component shortage", "component becomes unavailable", "component unavailable" }, DisruptionType.ComponentShortage),
        (new[] { "cyber", "ransomware", "cyberattack" }, DisruptionType.CyberIncident),
        (new[] { "earthquake", "flood", "hurricane", "natural disaster" }, DisruptionType.NaturalDisaster),
        (new[] { "labor", "strike", "walkout" }, DisruptionType.LaborDisruption),
    };

    public ScenarioParseResult Parse(string text, IReadOnlyList<(Guid Id, string Name, NodeType Type)> candidateNodes)
    {
        var clarifications = new List<string>();
        var lower = text.ToLowerInvariant();

        // --- Node ---
        var matches = candidateNodes
            .Where(n => lower.Contains(n.Name.ToLowerInvariant()))
            .ToList();

        (Guid Id, string Name, NodeType Type)? node = matches.Count switch
        {
            1 => matches[0],
            0 => null,
            _ => null // ambiguous - multiple known node names appear in the text
        };

        if (node is null)
        {
            clarifications.Add(matches.Count == 0
                ? "Which supplier, factory, or warehouse should this scenario target? I couldn't match any known node name in your description."
                : $"Multiple known nodes matched your description ({string.Join(", ", matches.Select(m => m.Name))}) - please specify exactly one.");
        }

        // --- Duration ---
        int? durationDays = null;
        var durationMatch = DurationPattern.Match(lower);
        if (durationMatch.Success)
        {
            var value = int.Parse(durationMatch.Groups[1].Value);
            var unit = durationMatch.Groups[2].Value;
            durationDays = unit.StartsWith("week") ? value * 7 : unit.StartsWith("month") ? value * 30 : value;
        }
        else
        {
            clarifications.Add("How long does the disruption last (e.g. \"30 days\")?");
        }

        // --- Severity ---
        double? severity = null;
        var severityMatch = SeverityPattern.Match(lower);
        if (severityMatch.Success)
        {
            severity = double.Parse(severityMatch.Groups[1].Value);
        }
        else if (ShutdownPattern.IsMatch(lower) || lower.Contains("unavailable"))
        {
            // A full shutdown/closure implies 100% severity - this is the one
            // case where inference is safe, because "shut down" has no other
            // plausible reading. Anything less explicit is not guessed.
            severity = 100;
        }
        else
        {
            clarifications.Add("What capacity reduction should this disruption cause (e.g. \"50%\" or \"100%\" for a full shutdown)?");
        }

        // --- Disruption type ---
        DisruptionType? type = null;
        if (ShutdownPattern.IsMatch(lower))
        {
            // Refine the generic "shutdown" signal using the matched node's
            // type - a shut-down supplier, factory, and warehouse are
            // different disruption types even though the language is the same.
            type = node?.Type switch
            {
                NodeType.Factory => DisruptionType.FactoryShutdown,
                NodeType.Warehouse => DisruptionType.WarehouseFailure,
                _ => DisruptionType.SupplierShutdown
            };
        }
        else
        {
            foreach (var (keywords, candidateType) in TypeKeywords)
            {
                if (keywords.Any(k => lower.Contains(k)))
                {
                    type = candidateType;
                    break;
                }
            }
        }

        if (type is null)
        {
            clarifications.Add("What kind of disruption is this (shutdown, capacity reduction, transportation failure, demand spike, etc.)?");
        }

        if (clarifications.Any() || node is null || durationDays is null || severity is null || type is null)
            return new ScenarioParseResult(null, clarifications);

        return new ScenarioParseResult(
            new ParsedScenario(node.Value.Id, node.Value.Name, type.Value, durationDays.Value, severity.Value),
            new List<string>());
    }
}
