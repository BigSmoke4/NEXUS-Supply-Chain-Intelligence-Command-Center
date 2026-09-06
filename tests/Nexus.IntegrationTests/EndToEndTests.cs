using System.Net;
using Xunit;

namespace Nexus.IntegrationTests;

public class HealthCheckTests : IClassFixture<NexusWebApplicationFactory>
{
    private readonly NexusWebApplicationFactory _factory;
    public HealthCheckTests(NexusWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_WithRealPostgres_ReturnsOk()
    {
        // This specifically exercises db.Database.CanConnectAsync() against
        // the real Testcontainers Postgres instance - the one check that is
        // categorically impossible to perform meaningfully with the
        // InMemory provider used by the unit test suite.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}


public class AuthenticationFlowTests : IClassFixture<NexusWebApplicationFactory>
{
    private readonly NexusWebApplicationFactory _factory;
    public AuthenticationFlowTests(NexusWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task UnauthenticatedRequest_ToCommandCenter_RedirectsToLogin()
    {
        // Proves the RBAC [Authorize] gate is actually enforced by the real
        // middleware pipeline against a real request, not just present as an
        // attribute that unit tests never exercise.
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/CommandCenter/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task RegisterThenAccessCommandCenter_Succeeds()
    {
        var client = _factory.CreateClient();

        // Fetch the register page first to obtain a valid antiforgery token,
        // the same way a real browser session would.
        var getResponse = await client.GetAsync("/Account/Register");
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(html);

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["email"] = $"integration-test-{Guid.NewGuid():N}@example.com",
            ["password"] = "IntegrationTest123!",
            ["displayName"] = "Integration Test User"
        });

        var registerResponse = await client.PostAsync("/Account/Register", formData);
        Assert.True(registerResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);

        var commandCenterResponse = await client.GetAsync("/CommandCenter/Index");
        Assert.Equal(HttpStatusCode.OK, commandCenterResponse.StatusCode);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        // Regex rather than a fixed-order substring match: ASP.NET Core's
        // tag helper doesn't guarantee attribute order, and this test has
        // never been run against real output to confirm what order it uses.
        var match = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""|value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        if (!match.Success) return string.Empty;
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }
}
