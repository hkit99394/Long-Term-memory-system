using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MemorySystem.Infrastructure.Configuration;

internal sealed class PostgresConnectionStringValidationHostedService(
    NpgsqlDataSource dataSource) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = dataSource.ConnectionString;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
