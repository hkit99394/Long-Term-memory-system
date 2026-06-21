using System.Diagnostics;
using MemorySystem.Api.Authentication;
using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Retention;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Observability;

namespace MemorySystem.Api.Admin;

public static class AdminGovernanceEndpointExtensions
{
    private const int DefaultGovernanceBatchSize = 100;
    private const int MaxGovernanceBatchSize = 500;
    private const int MaxLegalHoldListLimit = 100;
    private const int MaxRetentionReportLimit = 200;

    private static readonly IReadOnlySet<string> LegalHoldStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "active",
        "released"
    };

    public static IEndpointRouteBuilder MapMemorySystemAdminGovernanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/governance/legal-holds",
            async (
                HttpContext context,
                IAdminGovernanceStore store,
                CancellationToken cancellationToken) =>
                await ListLegalHoldsAsync(context, store, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/governance/legal-holds",
            async (
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IAdminGovernanceStore store) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/admin/governance/legal-holds",
                    async (idempotencyContext, cancellationToken) =>
                        await CreateLegalHoldAsync(context, store, idempotencyContext, cancellationToken)))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/governance/legal-holds/{id:guid}/release",
            async (
                Guid id,
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IAdminGovernanceStore store,
                IAuthenticationAuditRecorder authenticationAuditRecorder) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/admin/governance/legal-holds/release",
                    async (idempotencyContext, cancellationToken) =>
                        await ReleaseLegalHoldAsync(
                            id,
                            context,
                            store,
                            authenticationAuditRecorder,
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/governance/erasures",
            async (
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IAdminGovernanceStore store) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/admin/governance/erasures",
                    async (idempotencyContext, cancellationToken) =>
                        await ExecuteErasureAsync(context, store, idempotencyContext, cancellationToken)))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/governance/retention-report",
            async (
                HttpContext context,
                IAdminGovernanceStore store,
                CancellationToken cancellationToken) =>
                await ReadRetentionReportAsync(context, store, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> CreateLegalHoldAsync(
        HttpContext context,
        IAdminGovernanceStore store,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminGovernanceSelectionRequest>(
            context.Request,
            "Legal hold request is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var request = requestResult.Value!;
        if (!TryReadRequiredReason(request.Reason, out var reason, out var error)
            || !TryCreateSelector(idempotency.PrincipalId, request, requireSelector: true, out var selector, out error))
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Legal hold request is invalid.",
                error!);
        }

        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.GovernanceLegalHoldSpanName);
        activity?.SetTag("memorysystem.action", "create");
        TagGovernanceSelector(activity, selector);
        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, 0);

        AdminLegalHoldResult result;

        try
        {
            result = await store.CreateLegalHoldAsync(
                new AdminLegalHoldCreateCommand(
                    idempotency.PrincipalId,
                    idempotency.RecordId,
                    idempotency.RequestHash,
                    reason,
                    selector),
                cancellationToken);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "legal_hold_create_failed");
            throw;
        }

        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, result.MatchedEvents);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        return new ApiIdempotencyResponse(
            StatusCodes.Status201Created,
            ToResponse(result),
            "legal_hold",
            result.HoldId,
            IdempotencyAlreadyCompleted: true);
    }

    private static async Task<ApiIdempotencyResponse> ReleaseLegalHoldAsync(
        Guid holdId,
        HttpContext context,
        IAdminGovernanceStore store,
        IAuthenticationAuditRecorder authenticationAuditRecorder,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminLegalHoldReleaseRequest>(
            context.Request,
            "Legal hold release request is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        if (!TryReadRequiredReason(requestResult.Value!.Reason, out var reason, out var error))
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Legal hold release request is invalid.",
                error!);
        }

        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.GovernanceLegalHoldSpanName);
        activity?.SetTag("memorysystem.action", "release");
        activity?.SetTag(MemorySystemTelemetry.ScopeTypeAttribute, "unknown");
        activity?.SetTag("memorysystem.namespace", "unknown");
        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, 0);

        AdminLegalHoldReleaseResult result;

        try
        {
            result = await store.ReleaseLegalHoldAsync(
                new AdminLegalHoldReleaseCommand(
                    idempotency.PrincipalId,
                    idempotency.RecordId,
                    idempotency.RequestHash,
                    holdId,
                    reason),
                cancellationToken);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "legal_hold_release_failed");
            throw;
        }

        if (!result.Succeeded)
        {
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, result.FailureStatusCode.ToString());
            if (result.FailureStatusCode == StatusCodes.Status403Forbidden)
            {
                await authenticationAuditRecorder.RecordAuthorizationDeniedAsync(
                    context,
                    "legal_hold_release_forbidden",
                    cancellationToken,
                    "legal_hold",
                    holdId.ToString("D"));
            }

            return ApiRequestHelpers.Problem(
                result.FailureStatusCode,
                "Legal hold release request is invalid.",
                result.Error!);
        }

        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, result.ReleasedEvents + result.RestoredEvents);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        return new ApiIdempotencyResponse(
            StatusCodes.Status200OK,
            new AdminLegalHoldReleaseResponse(
                result.HoldId,
                result.Status,
                result.ReleasedEvents,
                result.RestoredEvents,
                result.ReleasedAt),
            "legal_hold",
            holdId,
            IdempotencyAlreadyCompleted: true);
    }

    private static async Task<ApiIdempotencyResponse> ExecuteErasureAsync(
        HttpContext context,
        IAdminGovernanceStore store,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminGovernanceSelectionRequest>(
            context.Request,
            "Erasure request is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var request = requestResult.Value!;
        if (!TryReadRequiredReason(request.Reason, out var reason, out var error)
            || !TryCreateSelector(idempotency.PrincipalId, request, requireSelector: true, out var selector, out error))
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Erasure request is invalid.",
                error!);
        }

        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.GovernanceErasureSpanName);
        TagGovernanceSelector(activity, selector);
        activity?.SetTag("memorysystem.events_redacted", 0);
        activity?.SetTag("memorysystem.derived_records_cleared", 0);

        AdminErasureExecutionResult result;

        try
        {
            result = await store.ExecuteErasureAsync(
                new AdminErasureExecutionCommand(
                    idempotency.PrincipalId,
                    idempotency.RecordId,
                    idempotency.RequestHash,
                    reason,
                    selector),
                cancellationToken);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "erasure_failed");
            throw;
        }

        activity?.SetTag("memorysystem.events_redacted", result.ErasedEvents);
        activity?.SetTag("memorysystem.derived_records_cleared", CountDerivedRecordsCleared(result));
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        return new ApiIdempotencyResponse(
            StatusCodes.Status200OK,
            ToResponse(result),
            "governance_erasure",
            result.AuditEventId,
            IdempotencyAlreadyCompleted: true);
    }

    private static async Task<IResult> ListLegalHoldsAsync(
        HttpContext context,
        IAdminGovernanceStore store,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!ApiRequestHelpers.TryReadLimitQuery(context, 50, MaxLegalHoldListLimit, out var limit, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Legal hold list request is invalid.",
                detail: error);
        }

        var status = NormalizeAllQuery(context, "status");
        if (!string.IsNullOrWhiteSpace(status) && !LegalHoldStatuses.Contains(status))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Legal hold list request is invalid.",
                detail: $"status must be one of: all, {string.Join(", ", LegalHoldStatuses)}.");
        }

        var records = await store.ListLegalHoldsAsync(
            new AdminLegalHoldListQuery(principalId, limit, status),
            cancellationToken);

        return Results.Ok(new AdminLegalHoldsResponse(records.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> ReadRetentionReportAsync(
        HttpContext context,
        IAdminGovernanceStore store,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!ApiRequestHelpers.TryReadLimitQuery(context, 100, MaxRetentionReportLimit, out var limit, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Retention report request is invalid.",
                detail: error);
        }

        if (!TryReadOptionalScopeQuery(context, out var scopeType, out var scopeId, out error)
            || !TryReadNamespacePrefixQuery(context, out var namespacePrefix, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Retention report request is invalid.",
                detail: error);
        }

        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.GovernanceRetentionReportSpanName);
        activity?.SetTag(MemorySystemTelemetry.ScopeTypeAttribute, scopeType ?? "all");
        activity?.SetTag("memorysystem.namespace", namespacePrefix ?? "all");
        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, 0);

        IReadOnlyList<AdminRetentionReportRecord> records;

        try
        {
            records = await store.ReadRetentionReportAsync(
                new AdminRetentionReportQuery(principalId, limit, scopeType, scopeId, namespacePrefix),
                cancellationToken);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "retention_report_failed");
            throw;
        }

        activity?.SetTag("memorysystem.retention_class", SummarizeValue(records.Select(record => record.RetentionClass)));
        activity?.SetTag("memorysystem.sensitivity", SummarizeValue(records.Select(record => record.Sensitivity)));
        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, records.Count);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        return Results.Ok(new AdminRetentionReportResponse(records.Select(ToResponse).ToArray()));
    }

    private static bool TryCreateSelector(
        Guid principalId,
        AdminGovernanceSelectionRequest request,
        bool requireSelector,
        out AdminGovernanceEventSelector selector,
        out string? error)
    {
        selector = null!;
        error = null;

        if (!TryReadSelectionScope(request.ScopeType, request.ScopeId, out var scopeType, out var scopeId, out error)
            || !TryReadNamespacePrefix(request.NamespacePrefix, out var namespacePrefix, out error)
            || !TryReadRetentionClass(request.RetentionClass, out var retentionClass, out error)
            || !TryReadSensitivity(request.Sensitivity, out var sensitivity, out error))
        {
            return false;
        }

        if (request.CreatedFrom.HasValue && request.CreatedTo.HasValue && request.CreatedFrom.Value > request.CreatedTo.Value)
        {
            error = "createdFrom must be earlier than or equal to createdTo.";
            return false;
        }

        var eventIds = request.EventIds?
            .Where(eventId => eventId != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (request.EventIds is not null && eventIds.Length != request.EventIds.Count)
        {
            error = "eventIds must not contain empty or duplicate ids.";
            return false;
        }

        if (eventIds.Length > MaxGovernanceBatchSize)
        {
            error = $"eventIds must contain {MaxGovernanceBatchSize} ids or fewer.";
            return false;
        }

        var maxEvents = request.MaxEvents ?? DefaultGovernanceBatchSize;
        if (maxEvents is < 1 or > MaxGovernanceBatchSize)
        {
            error = $"maxEvents must be between 1 and {MaxGovernanceBatchSize}.";
            return false;
        }

        var hasSelector = eventIds.Length > 0
            || !string.IsNullOrWhiteSpace(scopeType)
            || !string.IsNullOrWhiteSpace(namespacePrefix)
            || !string.IsNullOrWhiteSpace(retentionClass)
            || !string.IsNullOrWhiteSpace(sensitivity)
            || request.CreatedFrom.HasValue
            || request.CreatedTo.HasValue;

        if (requireSelector && !hasSelector)
        {
            error = "At least one selector is required.";
            return false;
        }

        selector = new AdminGovernanceEventSelector(
            principalId,
            maxEvents,
            eventIds,
            scopeType,
            scopeId,
            namespacePrefix,
            retentionClass,
            sensitivity,
            request.CreatedFrom,
            request.CreatedTo);
        return true;
    }

    private static bool TryReadRequiredReason(string? value, out string reason, out string? error)
    {
        reason = value?.Trim() ?? string.Empty;
        error = null;

        if (reason.Length == 0)
        {
            error = "reason is required.";
            return false;
        }

        if (reason.Length > 500)
        {
            error = "reason must be 500 characters or fewer.";
            return false;
        }

        return true;
    }

    private static bool TryReadOptionalScopeQuery(
        HttpContext context,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        return TryReadSelectionScope(
            NormalizeOptionalQuery(context, "scopeType"),
            NormalizeOptionalQuery(context, "scopeId"),
            out scopeType,
            out scopeId,
            out error);
    }

    private static bool TryReadSelectionScope(
        string? requestedScopeType,
        string? requestedScopeId,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        scopeType = null;
        scopeId = null;
        error = null;

        if (string.IsNullOrWhiteSpace(requestedScopeType) && string.IsNullOrWhiteSpace(requestedScopeId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestedScopeType) || string.IsNullOrWhiteSpace(requestedScopeId))
        {
            error = "scopeType and scopeId must be provided together.";
            return false;
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            requestedScopeType,
            requestedScopeId,
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

    private static bool TryReadNamespacePrefixQuery(
        HttpContext context,
        out string? namespacePrefix,
        out string? error)
    {
        return TryReadNamespacePrefix(
            NormalizeOptionalQuery(context, "namespacePrefix"),
            out namespacePrefix,
            out error);
    }

    private static bool TryReadNamespacePrefix(string? value, out string? namespacePrefix, out string? error)
    {
        namespacePrefix = value?.Trim();
        error = null;

        if (string.IsNullOrWhiteSpace(namespacePrefix))
        {
            namespacePrefix = null;
            return true;
        }

        if (!namespacePrefix.StartsWith("/", StringComparison.Ordinal))
        {
            error = "namespacePrefix must start with '/'.";
            return false;
        }

        return true;
    }

    private static bool TryReadRetentionClass(string? value, out string? retentionClass, out string? error)
    {
        retentionClass = NormalizeOptionalValue(value);
        error = null;

        if (string.IsNullOrWhiteSpace(retentionClass))
        {
            return true;
        }

        if (!MemoryRetentionClasses.All.Contains(retentionClass))
        {
            error = $"retentionClass must be one of: {string.Join(", ", MemoryRetentionClasses.All)}.";
            return false;
        }

        return true;
    }

    private static bool TryReadSensitivity(string? value, out string? sensitivity, out string? error)
    {
        sensitivity = NormalizeOptionalValue(value);
        error = null;

        if (string.IsNullOrWhiteSpace(sensitivity))
        {
            return true;
        }

        if (!MemoryScopePolicy.Sensitivities.Contains(sensitivity))
        {
            error = $"sensitivity must be one of: {string.Join(", ", MemoryScopePolicy.Sensitivities)}.";
            return false;
        }

        return true;
    }

    private static string? NormalizeOptionalQuery(HttpContext context, string key)
    {
        return NormalizeOptionalValue(context.Request.Query[key].ToString());
    }

    private static string? NormalizeAllQuery(HttpContext context, string key)
    {
        var value = NormalizeOptionalQuery(context, key);

        return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static void TagGovernanceSelector(Activity? activity, AdminGovernanceEventSelector selector)
    {
        activity?.SetTag(MemorySystemTelemetry.ScopeTypeAttribute, selector.ScopeType ?? "all");
        activity?.SetTag("memorysystem.namespace", selector.NamespacePrefix ?? "all");
        activity?.SetTag("memorysystem.retention_class", selector.RetentionClass ?? "all");
        activity?.SetTag("memorysystem.sensitivity", selector.Sensitivity ?? "all");
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");
    }

    private static int CountDerivedRecordsCleared(AdminErasureExecutionResult result)
    {
        return result.RedactedFacts
            + result.RedactedRoleLenses
            + result.RedactedChunks
            + result.StaleVaultExports
            + result.ClearedReviewNotes
            + result.RedactionRecords;
    }

    private static string SummarizeValue(IEnumerable<string> values)
    {
        var distinctValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();

        return distinctValues.Length switch
        {
            0 => "none",
            1 => distinctValues[0],
            _ => "mixed"
        };
    }

    private static AdminLegalHoldCreateResponse ToResponse(AdminLegalHoldResult result)
    {
        return new AdminLegalHoldCreateResponse(
            result.HoldId,
            result.Status,
            result.MatchedEvents,
            result.NewlyHeldEvents,
            result.AlreadyHeldEvents,
            result.Reason,
            result.CreatedAt);
    }

    private static AdminLegalHoldResponse ToResponse(AdminLegalHoldRecord record)
    {
        return new AdminLegalHoldResponse(
            record.Id,
            record.Status,
            record.Reason,
            record.ReleaseReason,
            record.CreatedByPrincipalId,
            record.ReleasedByPrincipalId,
            record.ScopeType,
            record.ScopeId,
            record.NamespacePrefix,
            record.RetentionClass,
            record.Sensitivity,
            record.CreatedFrom,
            record.CreatedTo,
            record.CreatedAt,
            record.ReleasedAt,
            record.EventCount,
            record.ActiveEventCount,
            record.ReleasedEventCount);
    }

    private static AdminErasureExecutionResponse ToResponse(AdminErasureExecutionResult result)
    {
        return new AdminErasureExecutionResponse(
            result.MatchedEvents,
            result.ErasedEvents,
            result.HeldEvents,
            result.RedactedFacts,
            result.RedactedRoleLenses,
            result.RedactedChunks,
            result.StaleVaultExports,
            result.ClearedReviewNotes,
            result.RedactionRecords,
            result.AuditEventId,
            result.ExecutedAt);
    }

    private static AdminRetentionReportRowResponse ToResponse(AdminRetentionReportRecord record)
    {
        return new AdminRetentionReportRowResponse(
            record.Namespace,
            record.RetentionClass,
            record.Sensitivity,
            record.AgeBucket,
            record.EventCount,
            record.LegalHoldEvents,
            record.ErasureRequestedEvents,
            record.RedactedEvents,
            record.OldestCreatedAt,
            record.NewestCreatedAt);
    }
}
