using System.Net;
using System.Text.Json;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAdminPilotReadinessTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public async Task Get_admin_pilot_readiness_requires_authentication()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/admin/pilot/readiness");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_admin_pilot_readiness_returns_payload_safe_go_status()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/admin/pilot/readiness");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("authorized source evidence raw payload", body, StringComparison.Ordinal);

        var root = payload.RootElement;
        Assert.Equal("external-pilot", root.GetProperty("scope").GetString());
        Assert.True(root.GetProperty("payloadSafe").GetBoolean());
        Assert.False(root.GetProperty("rawSourcePayloadsIncluded").GetBoolean());

        var decision = root.GetProperty("decision");
        Assert.Equal("go", decision.GetProperty("status").GetString());
        Assert.True(decision.GetProperty("externalInviteApproved").GetBoolean());
        Assert.Equal(
            "docs/external-pilot-go-epr04-v1.0.0-2026-06-04.md",
            decision.GetProperty("record").GetString());

        var gates = root.GetProperty("gates")
            .EnumerateArray()
            .ToDictionary(gate => gate.GetProperty("id").GetString()!);

        Assert.Equal("done", gates["EPR-04"].GetProperty("status").GetString());
        Assert.False(gates["EPR-04"].GetProperty("blocksExternalInvite").GetBoolean());
        Assert.Equal("done", gates["EPR-07"].GetProperty("status").GetString());

        var missingInputs = gates["EPR-04"]
            .GetProperty("missingInputs")
            .EnumerateArray()
            .Select(input => input.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(missingInputs);

        var nextWork = root.GetProperty("nextRecommendedWork")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("V1-01", nextWork);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", TestApiKey);
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return MemorySystemApiTestFactory.Create(
                "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused",
                TestApiKey,
                PrincipalId.ToString("D"))
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IPrincipalResolver>(new TestPrincipalResolver());
                });
            });
    }

    private sealed class TestPrincipalResolver : IPrincipalResolver
    {
        public Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
            ApiKeyPrincipalResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AuthenticatedPrincipal?>(
                request.PrincipalId == PrincipalId
                    ? new AuthenticatedPrincipal(
                        request.PrincipalId,
                        "human",
                        request.DisplayName,
                        AuthenticationMethods.ApiKey,
                        request.ApiKeyId)
                    : null);
        }

        public Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
            IdentityBindingLookup lookup,
            string authMethod = AuthenticationMethods.Oidc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AuthenticatedPrincipal?>(null);
        }
    }
}
