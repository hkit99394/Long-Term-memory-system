using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var options = OutboxWorkerOptions.Read(builder.Configuration);

builder.Services.AddSingleton(Options.Create(options));

if (options.Enabled)
{
    var connectionString = PostgresConnectionString.Resolve(
        key => builder.Configuration[key],
        builder.Configuration.GetConnectionString("Postgres"),
        builder.Environment.EnvironmentName);

    builder.Services.AddSingleton<IOutboxJobStore>(_ => new PostgresOutboxJobStore(connectionString));
    builder.Services.AddSingleton<OutboxJobProcessor>();
    builder.Services.AddHostedService<OutboxWorkerService>();
}

await builder.Build().RunAsync();
