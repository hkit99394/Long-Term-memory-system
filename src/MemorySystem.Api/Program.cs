using MemorySystem.Api.Access;
using MemorySystem.Api.Authentication;
using MemorySystem.Api.Events;
using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Api.MemoryFacts;
using MemorySystem.Api.MemoryProposals;
using MemorySystem.Api.Scopes;
using MemorySystem.Infrastructure.Configuration;
using MemorySystem.Infrastructure.Health;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemorySystemPostgresDataSource(builder.Configuration, builder.Environment);
builder.Services.AddMemorySystemEmbeddings(builder.Configuration);
builder.Services.AddMemorySystemApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddMemorySystemApiIdempotency(builder.Configuration, builder.Environment);
builder.Services.AddMemorySystemScopes();
builder.Services.AddMemorySystemAccess();
builder.Services.AddMemorySystemEvents(builder.Configuration, builder.Environment);
builder.Services.AddMemorySystemMemoryFacts();
builder.Services.AddMemorySystemMemoryProposals(builder.Configuration, builder.Environment);
builder.Services.ConfigureOptions<MemorySystemForwardedHeadersOptionsSetup>();

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddMemorySystemPostgres()
    .AddMemorySystemOutboxBacklog();

var app = builder.Build();

if (RequiresTransportSecurity(app.Environment))
{
    app.UseForwardedHeaders();
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("HTTPS is required for this API.", context.RequestAborted);
            return;
        }

        await next(context);
    });
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

app.MapGet("/", () => "Hello World!").AllowAnonymous();
app.MapMemorySystemEventEndpoints();
app.MapMemorySystemMemoryFactEndpoints();
app.MapMemorySystemMemoryProposalEndpoints();

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

                    if (string.IsNullOrWhiteSpace(request.Value))
                    {
                        return ApiRequestHelpers.Problem(
                            StatusCodes.Status400BadRequest,
                            "Widget request is invalid.",
                            "value is required.");
                    }

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
            status = entry.Value.Status.ToString()
        })
    });
}

static bool RequiresTransportSecurity(IHostEnvironment environment)
{
    return !environment.IsDevelopment()
        && !environment.IsEnvironment("Testing");
}

public partial class Program
{
}

internal sealed record TestIdempotencyRequest(string Value);

internal sealed record TestIdempotencyResponse(Guid Id, string Value);
