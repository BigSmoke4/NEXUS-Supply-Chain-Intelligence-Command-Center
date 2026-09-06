using Nexus.Web.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nexus.Web.Modules.Reporting;

/// <summary>
/// Generates the Executive Summary PDF (§59) from a computed ScenarioResult.
/// Uses QuestPDF (MIT/community-licensed, no external service call needed -
/// important in an environment with no network access to verify against a
/// paid rendering API). Every figure printed here is read directly off
/// ScenarioResult/MitigationStrategy - this module formats, it never
/// computes (§71 applies to reports exactly as it does to the UI).
/// </summary>
public interface IExecutiveSummaryReportGenerator
{
    byte[] Generate(Scenario scenario);
}

public class QuestPdfExecutiveSummaryReportGenerator : IExecutiveSummaryReportGenerator
{
    public QuestPdfExecutiveSummaryReportGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(Scenario scenario)
    {
        var result = scenario.LastResult
            ?? throw new InvalidOperationException("Cannot generate a report for a scenario with no computed result.");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("NEXUS — Executive Summary").FontSize(20).Bold();
                    col.Item().Text(scenario.Name).FontSize(14).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(4).Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Element(c => KeyValueRow(c, "Impact", result.OverallRiskScore >= 70 ? "HIGH" : result.OverallRiskScore >= 40 ? "MEDIUM" : "LOW"));
                    col.Item().Element(c => KeyValueRow(c, "Revenue at Risk", $"${result.RevenueAtRisk:N0}"));
                    col.Item().Element(c => KeyValueRow(c, "Products Affected", result.ProductsAffected.ToString()));
                    col.Item().Element(c => KeyValueRow(c, "Customers Affected", result.CustomersAffected.ToString()));
                    col.Item().Element(c => KeyValueRow(c, "Projected Recovery", $"{result.RecoveryDays} days"));
                    col.Item().Element(c => KeyValueRow(c, "Service Level", $"{result.ServiceLevelPercent}%"));
                    col.Item().Element(c => KeyValueRow(c, "Overall Risk Score", result.OverallRiskScore.ToString()));

                    if (result.Strategies.Any())
                    {
                        var best = result.Strategies.OrderBy(s => s.EstimatedRevenueLossAfter).First();
                        col.Item().PaddingTop(10).Text("Recommended Action").Bold().FontSize(13);
                        col.Item().Text(best.Description);
                        col.Item().Element(c => KeyValueRow(c, "Expected Revenue Loss After Mitigation", $"${best.EstimatedRevenueLossAfter:N0}"));
                        col.Item().Element(c => KeyValueRow(c, "Additional Cost", $"${best.AdditionalCost:N0}"));
                        col.Item().Element(c => KeyValueRow(c, "Recovery After Mitigation", $"{best.RecoveryDaysAfter} days"));
                        col.Item().Element(c => KeyValueRow(c, "Confidence", $"{best.ConfidencePercent}%"));
                    }

                    if (result.Timeline.Any())
                    {
                        col.Item().PaddingTop(10).Text("Timeline").Bold().FontSize(13);
                        foreach (var t in result.Timeline.Take(10))
                            col.Item().Text($"Day {t.DayOffset}: {t.Label}").FontSize(10);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("NEXUS Supply-Chain Intelligence Command Center — ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span("figures computed by the deterministic simulation engine, not estimated for this report.").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void KeyValueRow(QuestPDF.Infrastructure.IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.ConstantItem(220).Text(label).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().Text(value).Bold();
        });
    }
}
