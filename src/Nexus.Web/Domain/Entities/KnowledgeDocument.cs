namespace Nexus.Web.Domain.Entities;

/// <summary>
/// An ingested enterprise document (supplier contract, procurement policy,
/// transportation agreement, product spec, risk report - §42). Content is
/// stored as plain text for retrieval; a production deployment would add a
/// blob-storage pointer for the original file alongside this extracted text.
/// </summary>
public class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = default!;
    public string DocumentType { get; set; } = default!; // Contract, Policy, TransportationAgreement, ProductSpec, RiskReport
    public string Content { get; set; } = default!;
    public Guid? RelatedSupplierId { get; set; }
    public DateTime IngestedAtUtc { get; set; } = DateTime.UtcNow;
}
