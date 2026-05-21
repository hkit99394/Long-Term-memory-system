using MemorySystem.Api.Authentication;
using MemorySystem.Api.Events;
using MemorySystem.Api.Idempotency;
using MemorySystem.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemorySystemApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddMemorySystemApiIdempotency(builder.Configuration, builder.Environment);
builder.Services.AddMemorySystemEvents(builder.Configuration, builder.Environment);

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
app.MapMemorySystemEventEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/__test/auth/fallback", (HttpContext context) => Results.Ok(new
    {
        authenticated = context.User.Identity?.IsAuthenticated == true,
        scheme = context.User.Identity?.AuthenticationType,
        nameIdentifier = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        name = context.User.FindFirst(ClaimTypes.Name)?.Value
    })).ExcludeFromDescription();

    app.MapPost(
        "/__test/idempotency/widgets",
        async (HttpContext context, ApiIdempotencyHttpService idempotency) =>
            await idempotency.ExecuteAsync(
                context,
                "POST /__test/idempotency/widgets",
                async cancellationToken =>
                {
                    var request = await context.Request.ReadFromJsonAsync<TestIdempotencyRequest>(cancellationToken)
                        ?? new TestIdempotencyRequest(string.Empty);
                    var resourceId = Guid.NewGuid();

                    return new ApiIdempotencyResponse(
                        StatusCodes.Status201Created,
                        new TestIdempotencyResponse(resourceId, request.Value),
                        "test_widget",
                        resourceId);
                }))
        .ExcludeFromDescription();
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

internal sealed record TestIdempotencyRequest(string Value);

internal sealed record TestIdempotencyResponse(Guid Id, string Value);
