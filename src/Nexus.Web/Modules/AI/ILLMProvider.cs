namespace Nexus.Web.Modules.AI;

/// <summary>
/// Vendor-agnostic chat completion port (§39). Business logic depends only on
/// this interface; concrete adapters (Anthropic, OpenAI, Azure, local) live in
/// Infrastructure and are swapped via DI. No controller, agent, or domain
/// service may reference a vendor SDK directly.
/// </summary>
public interface ILLMProvider
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

/// <summary>
/// No-op provider used when no API key is configured, and always used in this
/// sandbox build (no outbound network access here). It returns a clearly
/// labelled placeholder rather than fabricating a plausible-looking answer -
/// per §52, the deterministic core (graph, simulation, risk, mitigation) must
/// keep working even when the AI layer is unavailable.
/// </summary>
public class MockLlmProvider : ILLMProvider
{
    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        return Task.FromResult(
            "[AI provider not configured] This environment has no live LLM connection. " +
            "Wire a real ILLMProvider (Anthropic/OpenAI/Azure) in Program.cs to enable " +
            "natural-language scenario creation and narrative explanations. All numeric " +
            "results shown elsewhere come from the deterministic simulation engine and " +
            "are unaffected by this.");
    }
}

/// <summary>
/// Example real adapter shape (not wired up / not network-callable in this
/// sandbox). Register this instead of MockLlmProvider once an API key is
/// available, and call the real Anthropic Messages API per Anthropic's docs.
/// </summary>
public class AnthropicLlmProvider : ILLMProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public AnthropicLlmProvider(HttpClient http, string model = "claude-sonnet-5")
    {
        _http = http;
        _model = model;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } }
        };

        using var response = await _http.PostAsJsonAsync("/v1/messages", payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken: ct);
        return string.Join("\n", json?.Content.Select(c => c.Text) ?? Array.Empty<string>());
    }

    private record AnthropicResponse(List<AnthropicContentBlock> Content);
    private record AnthropicContentBlock(string Type, string Text);
}
