using System.Diagnostics;
using System.Text.Json;
using MemorySystem.Api.Http;
using MemorySystem.Infrastructure.Observability;
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

        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.ApiIdempotencySpanName);
        activity?.SetTag(MemorySystemTelemetry.CorrelationIdAttribute, MemorySystemTelemetry.ReadCorrelationId(httpContext.Items));
        activity?.SetTag("memorysystem.idempotency.endpoint", endpoint);
        SetIdempotencyTags(activity, "begin", resourceType: "unknown", failureStatus: "none");

        if (!ApiRequestHelpers.TryGetPrincipalId(httpContext, out var principalId))
        {
            SetIdempotencyTags(activity, "rejected", resourceType: "unknown", failureStatus: "unauthorized");
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authenticated principal is invalid.");
        }

        var idempotencyKeyResult = TryGetIdempotencyKey(httpContext.Request.Headers, options.Value);

        if (!idempotencyKeyResult.Success)
        {
            SetIdempotencyTags(activity, "rejected", resourceType: "unknown", failureStatus: "invalid_key");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid idempotency key.",
                detail: idempotencyKeyResult.Error);
        }

        var cancellationToken = httpContext.RequestAborted;
        ApiRequestHashes requestHashes;

        try
        {
            requestHashes = await requestHasher.ComputeHashAsync(httpContext.Request, cancellationToken);
        }
        catch (ApiRequestBodyTooLargeException exception)
        {
            SetIdempotencyTags(activity, "rejected", resourceType: "unknown", failureStatus: "request_body_too_large");
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
            requestHashes.CurrentHash,
            requestHashes.AcceptedHashes,
            expiresAt,
            cancellationToken);

        return beginResult.Status switch
        {
            ApiIdempotencyBeginStatus.Started => await ExecuteAndStoreAsync(
                principalId,
                beginResult.Record,
                requestHashes.CurrentHash,
                operation,
                activity,
                cancellationToken),
            ApiIdempotencyBeginStatus.Replay => ReplayStoredResult(beginResult.Record, activity),
            ApiIdempotencyBeginStatus.Conflict => IdempotencyProblem(
                activity,
                "conflict",
                "Idempotency key conflict.",
                "The idempotency key was already used for this principal and endpoint with a different request body."),
            ApiIdempotencyBeginStatus.Processing => IdempotencyProblem(
                activity,
                "processing",
                "Idempotency request is still processing.",
                "Retry this request after the original attempt finishes."),
            ApiIdempotencyBeginStatus.PreviousFailed => IdempotencyProblem(
                activity,
                "previous_failed",
                "Idempotency request failed before a response was recorded.",
                "Use a new idempotency key to retry the operation."),
            _ => IdempotencyProblem(
                activity,
                "rejected",
                "Idempotency request could not be accepted.",
                "The idempotency request state is unsupported.")
        };
    }

    private async Task<IResult> ExecuteAndStoreAsync(
        Guid principalId,
        ApiIdempotencyRecord record,
        string requestHash,
        Func<ApiIdempotencyExecutionContext, CancellationToken, Task<ApiIdempotencyResponse>> operation,
        Activity? activity,
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

            SetIdempotencyTags(
                activity,
                response.IdempotencyAlreadyCompleted ? "already_completed" : "completed",
                response.ResourceType,
                response.StatusCode >= StatusCodes.Status400BadRequest
                    ? response.StatusCode.ToString()
                    : "none");

            return new StoredJsonResult(response.StatusCode, responseBody, contentType);
        }
        catch
        {
            SetIdempotencyTags(activity, "failed", resourceType: "unknown", failureStatus: "operation_exception");
            activity?.SetStatus(ActivityStatusCode.Error);
            await store.MarkFailedAsync(record.Id, requestHash, CancellationToken.None);
            throw;
        }
    }

    private static IResult ReplayStoredResult(ApiIdempotencyRecord record, Activity? activity)
    {
        SetIdempotencyTags(
            activity,
            "replay",
            record.ResourceType ?? "unknown",
            failureStatus: "none");

        return new StoredJsonResult(
            record.ResponseStatus!.Value,
            record.ResponseBody,
            record.ResponseContentType
                ?? InferStoredContentType(record.ResponseStatus.Value, record.ResponseBody));
    }

    private static IResult IdempotencyProblem(
        Activity? activity,
        string status,
        string title,
        string detail)
    {
        SetIdempotencyTags(activity, status, resourceType: "unknown", failureStatus: status);

        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            detail: detail);
    }

    private static void SetIdempotencyTags(
        Activity? activity,
        string status,
        string? resourceType,
        string failureStatus)
    {
        activity?.SetTag("memorysystem.idempotency.status", status);
        activity?.SetTag("memorysystem.resource_type", string.IsNullOrWhiteSpace(resourceType) ? "unknown" : resourceType);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, failureStatus);
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
