using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Events;

namespace MemorySystem.Api.Events;

public static class ApiEventServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddMemorySystemSourceEventLinks();
        services.AddSingleton(serviceProvider =>
            ReadExternalPayloadUriPolicy(
                serviceProvider.GetRequiredService<IConfiguration>(),
                environment));
        services.AddSingleton<PostgresEventStore>();
        services.AddSingleton<IEventAppendWorkflow, EventAppendWorkflow>();
        services.AddSingleton<IEventReadService, EventReadService>();
        services.AddSingleton<IEventStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());
        services.AddSingleton<IEventReadStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());
        services.AddSingleton<ISourceEventReferenceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresEventStore>());

        return services;
    }

    private static ExternalPayloadUriPolicy ReadExternalPayloadUriPolicy(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection("Events:ExternalPayloadUri");

        var allowLocalFileUris = bool.TryParse(section["AllowLocalFileUris"], out var configuredAllowLocalFileUris)
            ? configuredAllowLocalFileUris
            : environment.IsDevelopment() || environment.IsEnvironment("Testing");

        return new ExternalPayloadUriPolicy(
            ReadConfiguredValues(section.GetSection("AllowedSchemes")),
            ReadConfiguredValues(section.GetSection("AllowedPrefixes")),
            allowLocalFileUris);
    }

    private static IEnumerable<string> ReadConfiguredValues(IConfigurationSection section)
    {
        var children = section.GetChildren().ToArray();

        return children.Length == 0
            ? SplitConfiguredValue(section.Value)
            : children.SelectMany(child => SplitConfiguredValue(child.Value));
    }

    private static IEnumerable<string> SplitConfiguredValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                new[] { ',', ';' },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
