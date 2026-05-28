using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Scopes;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Http;

internal static class ApiRequestHelpers
{
    public static bool TryGetPrincipalId(HttpContext context, out Guid principalId)
    {
        var principalIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(principalIdValue, out principalId);
    }

    public static bool TryReadPrincipalId(
        HttpContext context,
        out Guid principalId,
        [NotNullWhen(false)] out IResult? failure)
    {
        if (TryGetPrincipalId(context, out principalId))
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authenticated principal is invalid.",
            detail: "The API key did not resolve to a valid principal id.");
        return false;
    }

    public static bool TryReadLimitQuery(
        HttpContext context,
        int defaultLimit,
        int maxLimit,
        out int limit,
        out string? error)
    {
        limit = defaultLimit;
        error = null;
        var limitValue = context.Request.Query["limit"].ToString();

        if (string.IsNullOrWhiteSpace(limitValue))
        {
            return true;
        }

        if (!int.TryParse(limitValue, out limit) || limit < 1 || limit > maxLimit)
        {
            error = $"Query parameter 'limit' must be between 1 and {maxLimit}.";
            return false;
        }

        return true;
    }

    public static async Task<JsonBodyReadResult<T>> ReadJsonBodyAsync<T>(
        HttpRequest request,
        string title,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return JsonBodyReadResult<T>.Failure(
                Problem(
                    StatusCodes.Status400BadRequest,
                    title,
                    "Request Content-Type must be application/json."));
        }

        try
        {
            var value = await request.ReadFromJsonAsync<T>(cancellationToken);

            return value is null
                ? JsonBodyReadResult<T>.Failure(
                    Problem(
                        StatusCodes.Status400BadRequest,
                        title,
                        "Request body is required."))
                : JsonBodyReadResult<T>.Success(value);
        }
        catch (JsonException)
        {
            return JsonBodyReadResult<T>.Failure(
                Problem(
                    StatusCodes.Status400BadRequest,
                    title,
                    "Request body must be valid JSON."));
        }
    }

    public static ApiIdempotencyResponse Problem(int statusCode, string title, string detail)
    {
        return new ApiIdempotencyResponse(
            statusCode,
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            });
    }

    public static bool TryReadOptionalTargetScopeQuery(
        HttpContext context,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        scopeType = context.Request.Query["scopeType"].ToString();
        scopeId = context.Request.Query["scopeId"].ToString();
        error = null;

        if (string.IsNullOrWhiteSpace(scopeType) && string.IsNullOrWhiteSpace(scopeId))
        {
            scopeType = null;
            scopeId = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(scopeType) || string.IsNullOrWhiteSpace(scopeId))
        {
            error = "Query parameters 'scopeType' and 'scopeId' must be provided together.";
            return false;
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out error))
        {
            return false;
        }

        scopeType = normalizedScopeType;
        scopeId = normalizedScopeId;
        return true;
    }
}

internal sealed record JsonBodyReadResult<T>(T? Value, ApiIdempotencyResponse? Problem)
{
    public bool Succeeded => Problem is null;

    public static JsonBodyReadResult<T> Success(T value)
    {
        return new JsonBodyReadResult<T>(value, null);
    }

    public static JsonBodyReadResult<T> Failure(ApiIdempotencyResponse problem)
    {
        return new JsonBodyReadResult<T>(default, problem);
    }
}
