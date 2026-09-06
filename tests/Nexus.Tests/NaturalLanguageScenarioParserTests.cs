using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.NaturalLanguage;
using Xunit;

namespace Nexus.Tests;

public class NaturalLanguageScenarioParserTests
{
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid FactoryId = Guid.NewGuid();

    private static List<(Guid Id, string Name, NodeType Type)> Candidates() => new()
    {
        (SupplierId, "Supplier Alpha", NodeType.Supplier),
        (FactoryId, "Factory Beta", NodeType.Factory),
    };

    [Fact]
    public void Parse_FullShutdownSentence_ExtractsAllFieldsWithoutGuessing()
    {
        var parser = new NaturalLanguageScenarioParser();

        var result = parser.Parse("Simulate Supplier Alpha shutting down for 30 days", Candidates());

        Assert.True(result.Succeeded);
        Assert.Equal(SupplierId, result.Scenario!.NodeId);
        Assert.Equal(DisruptionType.SupplierShutdown, result.Scenario.Type);
        Assert.Equal(30, result.Scenario.DurationDays);
        Assert.Equal(100, result.Scenario.SeverityPercent); // "shut down" implies 100% - the one safe inference
    }

    [Fact]
    public void Parse_FactoryCapacityReduction_InfersFactoryShutdownTypeFromNodeType()
    {
        var parser = new NaturalLanguageScenarioParser();

        var result = parser.Parse("Factory Beta loses 50% capacity for 2 weeks", Candidates());

        Assert.True(result.Succeeded);
        Assert.Equal(FactoryId, result.Scenario!.NodeId);
        Assert.Equal(DisruptionType.FactoryCapacityReduction, result.Scenario.Type);
        Assert.Equal(50, result.Scenario.SeverityPercent);
        Assert.Equal(14, result.Scenario.DurationDays); // "2 weeks" -> 14 days
    }

    [Fact]
    public void Parse_MissingDuration_AsksForClarification_DoesNotGuess()
    {
        var parser = new NaturalLanguageScenarioParser();

        var result = parser.Parse("Supplier Alpha shuts down", Candidates());

        Assert.False(result.Succeeded);
        Assert.Contains(result.ClarificationsNeeded, c => c.Contains("how long", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_UnknownNode_AsksForClarification()
    {
        var parser = new NaturalLanguageScenarioParser();

        var result = parser.Parse("Some unlisted supplier shuts down for 10 days", Candidates());

        Assert.False(result.Succeeded);
        Assert.Contains(result.ClarificationsNeeded, c => c.Contains("couldn't match", StringComparison.OrdinalIgnoreCase));
    }
}
