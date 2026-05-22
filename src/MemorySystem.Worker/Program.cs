using MemorySystem.Worker;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemorySystemOutboxWorker(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
