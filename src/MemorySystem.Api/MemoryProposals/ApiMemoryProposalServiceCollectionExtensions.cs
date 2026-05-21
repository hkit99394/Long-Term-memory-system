using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.MemoryProposals;

namespace MemorySystem.Api.MemoryProposals;

public static class ApiMemoryProposalServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryProposals(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IMemoryProposalBroker, MinimalMemoryProposalBroker>();
        services.AddSingleton<IMemoryProposalWriteStore>(_ =>
        {
            var connectionString = PostgresConnectionString.Resolve(
                key => configuration[key],
                configuration.GetConnectionString("Postgres"),
                environment.EnvironmentName);

            return new PostgresMemoryProposalWriteStore(connectionString);
        });

        return services;
    }
}
