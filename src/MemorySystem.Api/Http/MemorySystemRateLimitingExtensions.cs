using System.Security.Claims;
using System.Threading.RateLimiting;
using MemorySystem.Api.Authentication;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.RateLimiting;

namespace MemorySystem.Api.Http;

public static class MemorySystemRateLimitingExtensions
{
    public static IServiceCollection AddMemorySystemRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            var limits = MemorySystemRateLimitOptions.Read(configuration);

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = CreatePartitionedLimiter(limits.Default);
            options.AddPolicy(
                MemorySystemRateLimitPolicyNames.Expensive,
                context => CreatePartition(context, limits.Expensive));
            options.AddPolicy(
                MemorySystemRateLimitPolicyNames.Mutation,
                context => CreatePartition(context, limits.Mutation));
            options.AddPolicy(
                MemorySystemRateLimitPolicyNames.Admin,
                context => CreatePartition(context, limits.Admin));
        });

        return services;
    }

    private static PartitionedRateLimiter<HttpContext> CreatePartitionedLimiter(
        MemorySystemRateLimitLimit limit)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(
            context => CreatePartition(context, limit));
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        MemorySystemRateLimitLimit limit)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            GetPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit.PermitLimit,
                Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                QueueLimit = limit.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    }

    private static string GetPartitionKey(HttpContext context)
    {
        var apiKeyId = context.User.FindFirst(ApiKeyAuthenticationDefaults.ApiKeyIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(apiKeyId))
        {
            return $"api-key:{apiKeyId}";
        }

        var principalId = context.User.FindFirst(MemorySystemClaimTypes.PrincipalId)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(principalId))
        {
            return $"principal:{principalId}";
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp)
            ? "anonymous:unknown"
            : $"ip:{remoteIp}";
    }
}

internal sealed record MemorySystemRateLimitOptions(
    MemorySystemRateLimitLimit Default,
    MemorySystemRateLimitLimit Expensive,
    MemorySystemRateLimitLimit Mutation,
    MemorySystemRateLimitLimit Admin)
{
    public static MemorySystemRateLimitOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");

        return new MemorySystemRateLimitOptions(
            ReadLimit(section.GetSection("Default"), permitLimit: 600, windowSeconds: 60),
            ReadLimit(section.GetSection("Expensive"), permitLimit: 60, windowSeconds: 60),
            ReadLimit(section.GetSection("Mutation"), permitLimit: 120, windowSeconds: 60),
            ReadLimit(section.GetSection("Admin"), permitLimit: 30, windowSeconds: 60));
    }

    private static MemorySystemRateLimitLimit ReadLimit(
        IConfigurationSection section,
        int permitLimit,
        int windowSeconds)
    {
        return new MemorySystemRateLimitLimit(
            ReadPositiveInt(section, "PermitLimit", permitLimit),
            ReadPositiveInt(section, "WindowSeconds", windowSeconds),
            ReadNonNegativeInt(section, "QueueLimit", 0));
    }

    private static int ReadPositiveInt(IConfiguration section, string key, int fallback)
    {
        return int.TryParse(section[key], out var value) && value > 0 ? value : fallback;
    }

    private static int ReadNonNegativeInt(IConfiguration section, string key, int fallback)
    {
        return int.TryParse(section[key], out var value) && value >= 0 ? value : fallback;
    }
}

internal sealed record MemorySystemRateLimitLimit(
    int PermitLimit,
    int WindowSeconds,
    int QueueLimit);
