using System.Security.Claims;
using System.Text.Json;
using MemorySystem.Api.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Http;

internal static class ApiRequestHelpers
{
    public static bool TryGetPrincipalId(HttpContext context, out Guid principalId)
    {
        var principalIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(principalIdValue, out principalId);
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
