using MemorySystem.Application.Events;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MemorySystem.Api.Events;

internal static class ApiSourceEventLinkServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemSourceEventLinks(this IServiceCollection services)
    {
        services.TryAddSingleton<ISourceEventLinkBuilder, ApiSourceEventLinkBuilder>();

        return services;
    }
}
