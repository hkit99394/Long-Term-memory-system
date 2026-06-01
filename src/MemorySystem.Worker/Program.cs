using MemorySystem.Infrastructure.Observability;
using Microsoft.Extensions.Hosting;

namespace MemorySystem.Worker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddMemorySystemTelemetry(
            builder.Configuration,
            builder.Environment,
            "worker",
            includeAspNetCoreInstrumentation: false);
        builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment);
        builder.Services.AddMemorySystemEphemeralEventRetentionWorker(builder.Configuration, builder.Environment);

        await builder.Build().RunAsync();
    }
}
