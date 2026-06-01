using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MemorySystem.Infrastructure.Observability;

public sealed record MemorySystemTelemetryOptions(
    bool Enabled,
    string Exporter,
    string ServiceName,
    string ServiceVersion,
    string ServiceInstanceId,
    string EnvironmentName,
    string ServiceRole)
{
    public bool ExportToOtlp => string.Equals(Exporter, "otlp", StringComparison.OrdinalIgnoreCase);

    public static MemorySystemTelemetryOptions Read(
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceRole)
    {
        var section = configuration.GetSection("OpenTelemetry");

        var exporter = FirstConfigured(
            section[nameof(Exporter)],
            configuration["OTEL_TRACES_EXPORTER"],
            configuration["OTEL_METRICS_EXPORTER"],
            "none");

        return new MemorySystemTelemetryOptions(
            Enabled: ReadBool(section[nameof(Enabled)], fallback: true),
            Exporter: exporter,
            ServiceName: FirstConfigured(
                section[nameof(ServiceName)],
                configuration["OTEL_SERVICE_NAME"],
                MemorySystemTelemetry.ServiceName),
            ServiceVersion: FirstConfigured(
                section[nameof(ServiceVersion)],
                configuration["OTEL_SERVICE_VERSION"],
                configuration["MEMORYSYSTEM_SERVICE_VERSION"],
                ReadAssemblyVersion()),
            ServiceInstanceId: FirstConfigured(
                section[nameof(ServiceInstanceId)],
                configuration["OTEL_SERVICE_INSTANCE_ID"],
                configuration["HOSTNAME"],
                Environment.MachineName),
            EnvironmentName: FirstConfigured(
                section[nameof(EnvironmentName)],
                configuration["OTEL_DEPLOYMENT_ENVIRONMENT"],
                environment.EnvironmentName),
            ServiceRole: serviceRole);
    }

    private static bool ReadBool(string? value, bool fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : bool.Parse(value);
    }

    private static string FirstConfigured(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!.Trim();
    }

    private static string ReadAssemblyVersion()
    {
        var version = typeof(MemorySystemTelemetryOptions)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version) ? "0.0.0-dev" : version;
    }
}
