using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Alert_routing_contract_covers_pi06_routes_silences_and_environment_tests()
    {
        var root = FindRepositoryRoot();
        var routingJson = File.ReadAllText(Path.Combine(root, "observability", "alert-routing", "memorysystem-alert-routing.json"));
        var alertRules = File.ReadAllText(Path.Combine(root, "observability", "prometheus", "memorysystem-pilot-alerts.yml"));
        var observabilityMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-observability", "main.tf"));
        var observabilityVariables = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-observability", "variables.tf"));
        var observabilityDocs = File.ReadAllText(Path.Combine(root, "docs", "production-observability.md"));

        using var routing = JsonDocument.Parse(routingJson);
        var routeSeverities = routing.RootElement
            .GetProperty("routes")
            .EnumerateArray()
            .Select(route => route.GetProperty("severity").GetString())
            .ToHashSet(StringComparer.Ordinal);
        var routeTestEnvironments = routing.RootElement
            .GetProperty("environmentTestRoutes")
            .EnumerateArray()
            .Select(route => route.GetProperty("environment").GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("page", routeSeverities);
        Assert.Contains("ticket", routeSeverities);
        Assert.Contains("info", routeSeverities);
        Assert.Contains("pilot", routeTestEnvironments);
        Assert.Contains("production", routeTestEnvironments);
        Assert.True(routing.RootElement.GetProperty("silencingPolicy").GetProperty("requiresOwnerApproval").GetBoolean());

        foreach (var required in new[]
        {
            "route: page",
            "owner: memorysystem-oncall",
            "route: ticket",
            "owner: memorysystem-platform-maintainers",
            "route: info",
            "owner: memorysystem-release-owner",
            "runbook_url: docs/production-observability.md#"
        })
        {
            Assert.Contains(required, alertRules, StringComparison.Ordinal);
        }

        Assert.Contains("MemorySystemPilotAlertRouteTest", alertRules, StringComparison.Ordinal);
        Assert.Contains("MemorySystemProductionAlertRouteTest", alertRules, StringComparison.Ordinal);
        Assert.Contains("memorysystem_alert_route_test", alertRules, StringComparison.Ordinal);

        Assert.Contains("alert_routes", observabilityMain, StringComparison.Ordinal);
        Assert.Contains("alert_silence_policy", observabilityMain, StringComparison.Ordinal);
        Assert.Contains("alert_route_test", observabilityMain, StringComparison.Ordinal);
        Assert.Contains("alert-routing/memorysystem-alert-routing.json", observabilityMain, StringComparison.Ordinal);
        Assert.Contains("variable \"alert_route_owners\"", observabilityVariables, StringComparison.Ordinal);
        Assert.Contains("variable \"alert_route_test_enabled\"", observabilityVariables, StringComparison.Ordinal);

        foreach (var environment in new[] { "pilot", "production" })
        {
            var main = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", environment, "main.tf"));
            var variables = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", environment, "variables.tf"));
            var example = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", environment, "terraform.tfvars.example"));

            Assert.Contains("alert_route_owners       = var.alert_route_owners", main, StringComparison.Ordinal);
            Assert.Contains("alert_silence_policy     = var.alert_silence_policy", main, StringComparison.Ordinal);
            Assert.Contains("alert_route_test_enabled = var.alert_route_test_enabled", main, StringComparison.Ordinal);
            Assert.Contains("variable \"alert_route_owners\"", variables, StringComparison.Ordinal);
            Assert.Contains("variable \"alert_silence_policy\"", variables, StringComparison.Ordinal);
            Assert.Contains("alert_route_owners", example, StringComparison.Ordinal);
            Assert.Contains("alert_route_test_enabled = true", example, StringComparison.Ordinal);
        }

        Assert.Contains("Alert Routing and Silencing", observabilityDocs, StringComparison.Ordinal);
        Assert.Contains("Alert Routing Test", observabilityDocs, StringComparison.Ordinal);
        Assert.Contains("page silences last at most 1 hour", observabilityDocs, StringComparison.Ordinal);
    }
}
