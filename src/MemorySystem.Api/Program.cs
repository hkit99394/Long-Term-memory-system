using MemorySystem.Api.Authentication;
using MemorySystem.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemorySystemApiAuthentication(builder.Configuration, builder.Environment);

builder.Services
    .AddHealthChecks()
    .AddMemorySystemPostgres();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

app.MapGet("/", () => "Hello World!").AllowAnonymous();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/__test/auth/fallback", (HttpContext context) => Results.Ok(new
    {
        authenticated = context.User.Identity?.IsAuthenticated == true,
        scheme = context.User.Identity?.AuthenticationType,
        nameIdentifier = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        name = context.User.FindFirst(ClaimTypes.Name)?.Value
    })).ExcludeFromDescription();
}

app.Run();

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
