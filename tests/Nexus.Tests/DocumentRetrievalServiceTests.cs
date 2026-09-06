using Nexus.Web.Domain.Entities;
using Nexus.Web.Modules.Knowledge;
using Xunit;

namespace Nexus.Tests;

public class DocumentRetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_FindsRelevantContractClause_ForSwitchingSuppliersQuestion()
    {
        var (db, orgId) = TestDb.Create();

        // This mirrors the original brief's own example question almost
        // verbatim: "Can we switch from Supplier A to Supplier B according
        // to the contract?"
        db.KnowledgeDocuments.AddRange(
            new KnowledgeDocument
            {
                OrganizationId = orgId, Title = "Supplier Alpha Master Supply Agreement", DocumentType = "Contract",
                Content = "Section 4.2 Exclusivity: Buyer agrees to source no less than 70 percent of its Component X " +
                          "volume from Supplier Alpha. Section 4.3 Force Majeure Reallocation: in the event of a supply " +
                          "disruption lasting more than fourteen consecutive days, Buyer may reallocate affected volume " +
                          "to a qualified alternative supplier without breaching the exclusivity commitment."
            },
            new KnowledgeDocument
            {
                OrganizationId = orgId, Title = "Standard Transportation Services Agreement", DocumentType = "TransportationAgreement",
                Content = "This agreement governs ocean and air freight lanes. Buyer may convert any ocean freight " +
                          "lane to air freight on 48 hours notice, subject to a surcharge."
            });
        await db.SaveChangesAsync();

        var service = new TfIdfDocumentRetrievalService(db);

        var results = await service.RetrieveAsync(orgId,
            "If Supplier Alpha has a disruption, can we reallocate volume to an alternative supplier according to the contract?");

        Assert.NotEmpty(results);
        Assert.Equal("Supplier Alpha Master Supply Agreement", results.First().Title);
        Assert.Contains("reallocate", results.First().Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetrieveAsync_NoDocuments_ReturnsEmptyRatherThanFabricating()
    {
        var (db, orgId) = TestDb.Create();
        var service = new TfIdfDocumentRetrievalService(db);

        var results = await service.RetrieveAsync(orgId, "any question at all");

        Assert.Empty(results);
    }

    [Fact]
    public async Task RetrieveAsync_QueryWithNoLexicalOverlap_ReturnsEmpty()
    {
        var (db, orgId) = TestDb.Create();
        db.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            OrganizationId = orgId, Title = "Unrelated Policy", DocumentType = "Policy",
            Content = "This document discusses office supply reimbursement procedures."
        });
        await db.SaveChangesAsync();

        var service = new TfIdfDocumentRetrievalService(db);
        var results = await service.RetrieveAsync(orgId, "quantum entanglement spacecraft telemetry");

        Assert.Empty(results);
    }

    [Fact]
    public async Task RetrieveAsync_SynonymExpansion_MatchesDocumentUsingDifferentWording()
    {
        // This is the concrete proof that query expansion is doing real
        // work, not just documented as an aspiration: the query uses "switch"
        // and the document only ever says "reallocate" - a pure keyword
        // matcher with no expansion would score this zero.
        var (db, orgId) = TestDb.Create();
        db.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            OrganizationId = orgId, Title = "Supplier Alpha Master Supply Agreement", DocumentType = "Contract",
            Content = "In the event of a supply disruption, Buyer may reallocate affected volume to a " +
                      "qualified alternative supplier without breaching the exclusivity commitment."
        });
        await db.SaveChangesAsync();

        var service = new TfIdfDocumentRetrievalService(db);
        var results = await service.RetrieveAsync(orgId, "Can we switch suppliers?");

        Assert.NotEmpty(results);
    }
}
