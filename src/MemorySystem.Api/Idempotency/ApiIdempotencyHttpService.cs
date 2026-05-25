using System.Security.Claims;
using System.Text.Json;
using MemorySystem.Infrastructure.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.Idempotency;

public sealed class ApiIdempotencyHttpService(
    IApiIdempotencyStore store,
    IApiRequestHasher requestHasher,
    IOptions<ApiIdempotencyOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IResult> ExecuteAsync(
        HttpContext httpContext,
        string endpoint,
        Func<CancellationToken, Task<ApiIdempotencyResponse>> operation)
    {
        return await ExecuteAsync(
            httpContext,
            endpoint,
            (_, cancellationToken) => operation(cancellationToken));
    }

    public async Task<IResult> ExecuteAsync(
        HttpContext httpContext,
        string endpoint,
        Func<ApiIdempotencyExecutionContext, CancellationToken, Task<ApiIdempotencyResponse>> operation)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(operation);

        if (!TryGetPrincipalId(httpContext, out var principalId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authenticated principal is invalid.");
        }

        var idempotencyKeyResult = TryGetIdempotencyKey(httpContext.Request.Headers, options.Value);

        if (!idempotencyKeyResult.Success)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid idempotency key.",
                detail: idempotencyKeyResult.Error);
        }

        var cancellationToken = httpContext.RequestAborted;
        string requestHash;

        try
        {
            requestHash = await requestHasher.ComputeHashAsync(httpContext.Request, cancellationToken);
        }
        catch (ApiRequestBodyTooLargeException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Request body is too large.",
                detail: $"Mutating request bodies must be {exception.MaxBodyBytes} bytes or fewer.");
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(options.Value.RetentionPeriod);
        var beginResult = await store.BeginAsync(
            principalId,
            endpoint,
            idempotencyKeyResult.Key,
            requestHash,
            expiresAt,
            cancellationToken);

        return beginResult.Status switch
        {
            ApiIdempotencyBeginStatus.Started => await ExecuteAndStoreAsync(
                principalId,
                beginResult.Record,
                requestHash,
                operation,
                cancellationToken),
            ApiIdempotencyBeginStatus.Replay => new StoredJsonResult(
                beginResult.Record.ResponseStatus!.Value,
                beginResult.Record.ResponseBody,
                beginResult.Record.ResponseContentType
                    ?? InferStoredContentType(beginResult.Record.ResponseStatus.Value, beginResult.Record.ResponseBody)),
            ApiIdempotencyBeginStatus.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency key conflict.",
                detail: "The idempotency key was already used for this principal and endpoint with a different request body."),
            ApiIdempotencyBeginStatus.Processing => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency request is still processing.",
                detail: "Retry this request after the original attempt finishes."),
            ApiIdempotencyBeginStatus.PreviousFailed => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency request failed before a response was recorded.",
                detail: "Use a new idempotency key to retry the operation."),
            _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Idempotency request could not be accepted.")
        };
    }

    private async Task<IResult> ExecuteAndStoreAsync(
        Guid principalId,
        ApiIdempotencyRecord record,
        string requestHash,
        Func<ApiIdempotencyExecutionContext, CancellationToken, Task<ApiIdempotencyResponse>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await operation(
                new ApiIdempotencyExecutionContext(record.Id, requestHash, principalId),
                cancellationToken);
            var responseBody = response.Body is null
                ? null
                : JsonSerializer.Serialize(response.Body, JsonOptions);
            var contentType = response.ContentType ?? InferContentType(response.Body);

            if (!response.IdempotencyAlreadyCompleted)
            {
                await store.CompleteAsync(
                    record.Id,
                    requestHash,
                    response.StatusCode,
                    responseBody,
                    contentType,
                    response.ResourceType,
                    response.ResourceId,
                    cancellationToken);
            }

            return new StoredJsonResult(response.StatusCode, responseBody, contentType);
        }
        catch
        {
            await store.MarkFailedAsync(record.Id, requestHash, CancellationToken.None);
            throw;
        }
    }

    private static bool TryGetPrincipalId(HttpContext httpContext, out Guid principalId)
    {
        var principalIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(principalIdValue, out principalId);
    }

    private static string? InferContentType(object? body)
    {
        return body switch
        {
            null => null,
            ProblemDetails => "application/problem+json; charset=utf-8",
            _ => "application/json; charset=utf-8"
        };
    }

    private static string? InferStoredContentType(int responseStatus, string? responseBody)
    {
        if (responseBody is null)
        {
            return null;
        }

        return responseStatus >= StatusCodes.Status400BadRequest
            ? "application/problem+json; charset=utf-8"
            : "application/json; charset=utf-8";
    }

    private static IdempotencyKeyResult TryGetIdempotencyKey(
        IHeaderDictionary headers,
        ApiIdempotencyOptions options)
    {
        if (!headers.TryGetValue(options.HeaderName, out var values))
        {
            return IdempotencyKeyResult.Failure($"Send a single {options.HeaderName} header with mutating requests.");
        }

        if (values.Count != 1)
        {
            return IdempotencyKeyResult.Failure($"Send exactly one {options.HeaderName} header.");
        }

        var key = values[0];

        if (string.IsNullOrWhiteSpace(key))
        {
            return IdempotencyKeyResult.Failure($"{options.HeaderName} must not be blank.");
        }

        if (key.Length > options.MaxKeyLength)
        {
            return IdempotencyKeyResult.Failure($"{options.HeaderName} must be {options.MaxKeyLength} characters or fewer.");
        }

        return IdempotencyKeyResult.SuccessResult(key);
    }

    private sealed record IdempotencyKeyResult(bool Success, string Key, string? Error)
    {
        public static IdempotencyKeyResult SuccessResult(string key)
        {
            return new IdempotencyKeyResult(true, key, null);
        }

        public static IdempotencyKeyResult Failure(string error)
        {
            return new IdempotencyKeyResult(false, string.Empty, error);
        }
    }
}
