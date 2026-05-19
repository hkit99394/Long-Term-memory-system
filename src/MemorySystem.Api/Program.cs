using MemorySystem.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var postgresConnectionString = ResolvePostgresConnectionString(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddMemorySystemPostgres(postgresConnectionString);

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync
});

app.MapGet("/", () => "Hello World!");

app.Run();

static string ResolvePostgresConnectionString(IConfiguration configuration)
{
    var configuredConnectionString =
        configuration.GetConnectionString("Postgres") ??
        configuration["MEMORYSYSTEM_POSTGRES_CONNECTION_STRING"];

    if (!string.IsNullOrWhiteSpace(configuredConnectionString))
    {
        return configuredConnectionString;
    }

    var host = GetConfigurationValue(configuration, "MEMORYSYSTEM_POSTGRES_HOST", "localhost");
    var port = GetConfigurationValue(configuration, "MEMORYSYSTEM_POSTGRES_PORT", "5432");
    var database = GetConfigurationValue(configuration, "MEMORYSYSTEM_POSTGRES_DB", "memory_system");
    var username = GetConfigurationValue(configuration, "MEMORYSYSTEM_POSTGRES_USER", "memory_system");
    var password = GetConfigurationValue(configuration, "MEMORYSYSTEM_POSTGRES_PASSWORD", "memory_system_dev_password");

    return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

static string GetConfigurationValue(IConfiguration configuration, string key, string fallback)
{
    var value = configuration[key];

    return string.IsNullOrWhiteSpace(value) ? fallback : value;
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description
        })
    });
}

public partial class Program
{
}
