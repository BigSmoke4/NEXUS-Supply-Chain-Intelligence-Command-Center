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
/// Real Anthropic Messages API adapter. It is selected by configuration and
/// remains behind ILLMProvider so the rest of the application stays vendor
/// neutral. The API key is read only from configuration/user-secrets.
/// </summary>
public class AnthropicLlmProvider : ILLMProvider
{
    private readonly HttpClient _http;
    private readonly string _model;

    public AnthropicLlmProvider(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _model = configuration["AI:Model"] ?? "claude-sonnet-5";
        var apiKey = configuration["AI:AnthropicApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI:AnthropicApiKey must be configured when the Anthropic provider is selected.");

        _http.DefaultRequestHeaders.Remove("x-api-key");
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
