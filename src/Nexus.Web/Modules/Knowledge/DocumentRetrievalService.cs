using Nexus.Web.Data;
using Nexus.Web.Domain.Entities;

namespace Nexus.Web.Modules.Knowledge;

public record RetrievedPassage(Guid DocumentId, string Title, string DocumentType, string Snippet, double Score);

/// <summary>
/// Retrieval for the enterprise knowledge system (§42). This is deliberately
/// a TF-IDF-style lexical retriever, not a vector/embedding search - this
/// build has no network access to call an embeddings API, and a fake
/// "semantic search" that's secretly just keyword matching would be worse
/// than being upfront about it. Swap this implementation for a real
/// embedding-based retriever (e.g. pgvector + an embeddings endpoint) behind
/// the same IDocumentRetrievalService interface when you have one available;
/// nothing else in the AI pipeline needs to change.
///
/// Per §44, retrieved document content is treated as untrusted data: it is
/// only ever placed into the Evidence list as a quoted fact with a source
/// citation, never concatenated into the system prompt or given any
/// instruction-following authority. See AgentOrchestrator's system prompt
/// construction for where that boundary is enforced.
/// </summary>
public interface IDocumentRetrievalService
{
    Task<List<RetrievedPassage>> RetrieveAsync(Guid organizationId, string query, int topK = 3, CancellationToken ct = default);
}

public class TfIdfDocumentRetrievalService : IDocumentRetrievalService
{
    private readonly NexusDbContext _db;
    public TfIdfDocumentRetrievalService(NexusDbContext db) => _db = db;

    // Domain-specific query expansion: a real, standard information-retrieval
    // technique (not a claim of semantic understanding). It closes some of
    // the gap between "the user's words" and "the contract's words" - e.g.
    // asking to "switch" suppliers should also match a clause that says
    // "reallocate" - without pretending this is embedding-based similarity.
    // Expansion is applied to the query only, never to document content,
    // so it can't inflate a document's own term frequency.
    private static readonly Dictionary<string, string[]> SynonymExpansion = new()
    {
        ["switch"] = new[] { "reallocate", "transfer", "move" },
        ["switching"] = new[] { "reallocate", "reallocation", "transfer" },
        ["cancel"] = new[] { "terminate", "termination" },
        ["cancelling"] = new[] { "terminate", "termination" },
        ["disruption"] = new[] { "shutdown", "failure", "outage" },
        ["shutdown"] = new[] { "disruption", "closure" },
        ["expedite"] = new[] { "accelerate", "rush", "air freight" },
        ["alternate"] = new[] { "alternative", "backup", "substitute" },
        ["alternative"] = new[] { "alternate", "backup", "substitute" },
        ["exclusive"] = new[] { "exclusivity" },
        ["end"] = new[] { "terminate", "termination" },
    };

    public async Task<List<RetrievedPassage>> RetrieveAsync(Guid organizationId, string query, int topK = 3, CancellationToken ct = default)
    {
        var documents = _db.KnowledgeDocuments.Where(d => d.OrganizationId == organizationId).ToList();
        if (documents.Count == 0) return new List<RetrievedPassage>();

        var queryTerms = ExpandQuery(Tokenize(query));
        if (queryTerms.Count == 0) return new List<RetrievedPassage>();

        // Document frequency for IDF weighting across the corpus.
        var docTokenSets = documents.ToDictionary(d => d.Id, d => Tokenize(d.Content).ToHashSet());
        var docFrequency = queryTerms.ToDictionary(
            t => t,
            t => Math.Max(1, docTokenSets.Values.Count(set => set.Contains(t))));

        var scored = new List<RetrievedPassage>();
        foreach (var doc in documents)
        {
            var tokens = Tokenize(doc.Content);
            if (tokens.Count == 0) continue;

            var termFrequency = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            double score = 0;
            foreach (var term in queryTerms)
            {
                if (!termFrequency.TryGetValue(term, out var tf)) continue;
                // Smoothed IDF (the standard scikit-learn TfidfVectorizer
                // formula: log((N+1)/(df+1)) + 1) rather than raw log(N/df).
                // Raw IDF collapses to zero whenever a term appears in every
                // document in the corpus - which is guaranteed to happen with
                // a single-document corpus (N=1, df=1 -> log(1)=0), silently
                // zeroing every score and dropping matches that should have
                // ranked highest. Smoothing keeps IDF strictly positive.
                var idf = Math.Log((documents.Count + 1.0) / (docFrequency[term] + 1.0)) + 1.0;
                score += (tf / (double)tokens.Count) * idf;
            }

            if (score <= 0) continue;

            scored.Add(new RetrievedPassage(doc.Id, doc.Title, doc.DocumentType, ExtractSnippet(doc.Content, queryTerms), Math.Round(score, 4)));
        }

        return await Task.FromResult(scored.OrderByDescending(s => s.Score).Take(topK).ToList());
    }

    private static List<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '(', ')', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();

    private static List<string> ExpandQuery(List<string> terms)
    {
        var expanded = new List<string>(terms);
        foreach (var term in terms)
            if (SynonymExpansion.TryGetValue(term, out var synonyms))
                expanded.AddRange(synonyms.SelectMany(Tokenize));
        return expanded;
    }

    /// Returns the sentence with the highest query-term density, so the
    /// evidence entry quotes the most relevant part of a long document
    /// rather than always the opening line. Query terms are deduplicated so
    /// a repeated word in the question doesn't artificially inflate one
    /// sentence's hit count over another's.
    private static string ExtractSnippet(string content, List<string> queryTerms)
    {
        var distinctTerms = queryTerms.Distinct().ToList();
        var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var best = sentences
            .Select(s => (Sentence: s.Trim(), Hits: distinctTerms.Count(t => s.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(s => s.Hits)
            .FirstOrDefault();

        var snippet = string.IsNullOrWhiteSpace(best.Sentence) ? content : best.Sentence;
        return snippet.Length > 240 ? snippet[..240] + "…" : snippet;
    }
}
