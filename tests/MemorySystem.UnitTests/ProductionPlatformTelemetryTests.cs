using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Runtime_opentelemetry_wiring_matches_pi05_contract()
    {
        var root = FindRepositoryRoot();
        var apiProject = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "MemorySystem.Api.csproj"));
        var workerProject = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Worker", "MemorySystem.Worker.csproj"));
        var infrastructureProject = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "MemorySystem.Infrastructure.csproj"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Worker", "Program.cs"));
        var telemetry = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Observability", "MemorySystemTelemetry.cs"));
        var telemetryExtensions = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Observability", "MemorySystemTelemetryServiceCollectionExtensions.cs"));
        var telemetryOptions = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Observability", "MemorySystemTelemetryOptions.cs"));
        var correlationMiddleware = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Operations", "TelemetryCorrelationMiddleware.cs"));
        var apiMetrics = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Operations", "ApiRequestMetricsMiddleware.cs"));
        var outboxProcessor = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Worker", "OutboxJobProcessor.cs"));
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var runtimeVariables = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "variables.tf"));

        Assert.Contains("OpenTelemetry.Exporter.OpenTelemetryProtocol\" Version=\"1.15.3", apiProject, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Extensions.Hosting\" Version=\"1.15.3", workerProject, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Instrumentation.AspNetCore\" Version=\"1.15.1", infrastructureProject, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Instrumentation.Runtime\" Version=\"1.15.1", infrastructureProject, StringComparison.Ordinal);

        Assert.Contains("AddMemorySystemTelemetry(", apiProgram, StringComparison.Ordinal);
        Assert.Contains("includeAspNetCoreInstrumentation: true", apiProgram, StringComparison.Ordinal);
        Assert.Contains("UseMiddleware<TelemetryCorrelationMiddleware>()", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddMemorySystemTelemetry(", workerProgram, StringComparison.Ordinal);
        Assert.Contains("includeAspNetCoreInstrumentation: false", workerProgram, StringComparison.Ordinal);

        Assert.Contains("MemorySystem.Api.Request", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Api.Idempotency", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Retrieval.ContextPacket", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Retrieval.QueryFacts", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Review.Action", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.VaultExport.Render", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Worker.OutboxLease", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Worker.OutboxJob", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Governance.LegalHold", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Governance.Erasure", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Governance.RetentionReport", telemetry, StringComparison.Ordinal);

        Assert.Contains(".AddOpenTelemetry()", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("deployment.environment", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("serviceVersion: options.ServiceVersion", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("serviceInstanceId: options.ServiceInstanceId", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("AddAspNetCoreInstrumentation", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("AddHttpClientInstrumentation", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("AddRuntimeInstrumentation", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("AddNpgsql()", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("AddNpgsqlInstrumentation", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("AddOtlpExporter", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("WithLogging", telemetryExtensions, StringComparison.Ordinal);
        Assert.Contains("IncludeFormattedMessage = false", telemetryExtensions, StringComparison.Ordinal);

        Assert.Contains("OTEL_SERVICE_VERSION", telemetryOptions, StringComparison.Ordinal);
        Assert.Contains("OTEL_SERVICE_INSTANCE_ID", telemetryOptions, StringComparison.Ordinal);
        Assert.Contains("HOSTNAME", telemetryOptions, StringComparison.Ordinal);
        Assert.Contains("X-Correlation-ID", telemetry, StringComparison.Ordinal);
        Assert.Contains("memorysystem.correlation_id", telemetry, StringComparison.Ordinal);
        Assert.Contains("memorysystem_api_requests_total", telemetry, StringComparison.Ordinal);
        Assert.Contains("memorysystem_api_request_duration_ms", telemetry, StringComparison.Ordinal);
        Assert.Contains("MemorySystemTelemetry.ApiRequestCounter", apiMetrics, StringComparison.Ordinal);
        Assert.Contains("MemorySystemTelemetry.OutboxJobCounter", outboxProcessor, StringComparison.Ordinal);

        Assert.Contains("OpenTelemetry__Enabled", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry__Exporter", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry__ServiceVersion", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("trace_coverage_manifest", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("variable \"open_telemetry_exporter\"", runtimeVariables, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_telemetry_does_not_introduce_forbidden_payload_attributes()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "observability",
            "tracing",
            "memorysystem-pilot-trace-coverage.json")));

        var forbiddenAttributes = manifest.RootElement
            .GetProperty("forbiddenAttributes")
            .EnumerateArray()
            .Select(attribute => attribute.GetString()!)
            .ToArray();
        var telemetryFiles = new[]
        {
            Path.Combine(root, "src", "MemorySystem.Infrastructure", "Observability", "MemorySystemTelemetry.cs"),
            Path.Combine(root, "src", "MemorySystem.Infrastructure", "Observability", "MemorySystemTelemetryOptions.cs"),
            Path.Combine(root, "src", "MemorySystem.Infrastructure", "Observability", "MemorySystemTelemetryServiceCollectionExtensions.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "Operations", "TelemetryCorrelationMiddleware.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "Operations", "ApiRequestMetricsMiddleware.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "Idempotency", "ApiIdempotencyHttpService.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "MemoryFacts", "MemoryFactEndpointExtensions.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "MemoryReviews", "MemoryReviewEndpointExtensions.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "VaultExports", "VaultExportEndpointExtensions.cs"),
            Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminGovernanceEndpointExtensions.cs"),
            Path.Combine(root, "src", "MemorySystem.Worker", "OutboxJobProcessor.cs")
        };
        var sourceText = string.Join("\n", telemetryFiles.Select(File.ReadAllText));

        foreach (var forbiddenAttribute in forbiddenAttributes)
        {
            Assert.DoesNotContain(forbiddenAttribute, sourceText, StringComparison.Ordinal);
        }
    }
}
