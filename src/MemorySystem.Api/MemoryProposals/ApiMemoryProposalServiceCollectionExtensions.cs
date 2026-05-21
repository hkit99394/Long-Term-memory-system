using MemorySystem.Application.MemoryProposals;

namespace MemorySystem.Api.MemoryProposals;

public static class ApiMemoryProposalServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryProposals(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryProposalBroker, MinimalMemoryProposalBroker>();

        return services;
    }
}
