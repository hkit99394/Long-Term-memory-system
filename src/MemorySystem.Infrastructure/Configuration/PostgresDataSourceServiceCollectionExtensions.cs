using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MemorySystem.Infrastructure.Configuration;

public static class PostgresDataSourceServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemPostgresDataSource(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(_ =>
        {
            var connectionString = PostgresConnectionString.Resolve(
                key => configuration[key],
                configuration.GetConnectionString("Postgres"),
                environment.EnvironmentName);

            return NpgsqlDataSource.Create(connectionString);
        });

        return services;
    }
}
