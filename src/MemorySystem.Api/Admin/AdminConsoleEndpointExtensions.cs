using MemorySystem.Api.Http;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Retention;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Evidence;
using System.Globalization;

namespace MemorySystem.Api.Admin;

public static class AdminConsoleEndpointExtensions
{
    private const int MaxMemoryFactLimit = 100;
    private const int MaxSourceEventLimit = 100;

    private static readonly IReadOnlySet<string> EventTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "user_message",
        "assistant_message",
        "tool_call",
        "memory_proposed",
        "memory_written",
        "memory_deleted",
        "memory_redacted",
        "memory_reviewed"
    };

    private static readonly IReadOnlySet<string> RedactionStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "none",
        "pending",
        "redacted",
        "erased"
    };

    public static IEndpointRouteBuilder MapMemorySystemAdminConsoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/memory/facts",
            async (
                HttpContext context,
                IAdminMemoryInspectionStore store,
                ISourceEventLinkBuilder sourceEventLinks,
                CancellationToken cancellationToken) =>
                await ListMemoryFactsAsync(context, store, sourceEventLinks, cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/admin/source-events",
            async (
                HttpContext context,
                IAdminMemoryInspectionStore store,
                ISourceEventLinkBuilder sourceEventLinks,
                CancellationToken cancellationToken) =>
                await ListSourceEventsAsync(context, store, sourceEventLinks, cancellationToken))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ListMemoryFactsAsync(
        HttpContext context,
        IAdminMemoryInspectionStore store,
        ISourceEventLinkBuilder sourceEventLinks,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryCreateQuery(context, principalId, out var query, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Admin memory facts request is invalid.",
                detail: error);
        }

        var records = await store.ListMemoryFactsAsync(query, cancellationToken);

        return Results.Ok(new AdminMemoryFactsResponse(
            records.Select(record => ToResponse(record, sourceEventLinks)).ToArray()));
    }

    private static async Task<IResult> ListSourceEventsAsync(
        HttpContext context,
        IAdminMemoryInspectionStore store,
        ISourceEventLinkBuilder sourceEventLinks,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryCreateSourceEventQuery(context, principalId, out var query, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Admin source events request is invalid.",
                detail: error);
        }

        var records = await store.ListSourceEventsAsync(query, cancellationToken);

        return Results.Ok(new AdminSourceEventsResponse(
            records.Select(record => ToSourceEventResponse(record, sourceEventLinks)).ToArray()));
    }

    private static bool TryCreateQuery(
        HttpContext context,
        Guid principalId,
        out AdminMemoryFactListQuery query,
        out string? error)
    {
        query = null!;

        if (!ApiRequestHelpers.TryReadLimitQuery(context, defaultLimit: 50, maxLimit: MaxMemoryFactLimit, out var limit, out error))
        {
            return false;
        }

        var status = NormalizeOptionalQuery(context, "status");
        if (string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }

        if (!string.IsNullOrWhiteSpace(status) && !MemoryFactStatuses.IsSupported(status))
        {
            error = $"status must be one of: all, {string.Join(", ", MemoryFactStatuses.All)}.";
            return false;
        }

        var scopeType = NormalizeOptionalQuery(context, "scopeType");
        var scopeId = NormalizeOptionalQuery(context, "scopeId");
        if (string.IsNullOrWhiteSpace(scopeType) != string.IsNullOrWhiteSpace(scopeId))
        {
            error = "scopeType and scopeId must be provided together.";
            return false;
        }

        var namespacePrefix = NormalizeOptionalQuery(context, "namespacePrefix");
        if (!string.IsNullOrWhiteSpace(namespacePrefix) && !namespacePrefix.StartsWith("/", StringComparison.Ordinal))
        {
            error = "namespacePrefix must start with '/'.";
            return false;
        }

        query = new AdminMemoryFactListQuery(
            principalId,
            limit,
            status,
            scopeType,
            scopeId,
            NormalizeOptionalQuery(context, "memoryType"),
            namespacePrefix,
            NormalizeOptionalQuery(context, "q"));
        return true;
    }

    private static bool TryCreateSourceEventQuery(
        HttpContext context,
        Guid principalId,
        out AdminSourceEventListQuery query,
        out string? error)
    {
        query = null!;

        if (!ApiRequestHelpers.TryReadLimitQuery(context, defaultLimit: 50, maxLimit: MaxSourceEventLimit, out var limit, out error))
        {
            return false;
        }

        var scopeType = NormalizeOptionalQuery(context, "scopeType");
        var scopeId = NormalizeOptionalQuery(context, "scopeId");
        if (string.IsNullOrWhiteSpace(scopeType) != string.IsNullOrWhiteSpace(scopeId))
        {
            error = "scopeType and scopeId must be provided together.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(scopeType) && !MemoryScopePolicy.ScopeTypes.Contains(scopeType))
        {
            error = $"scopeType must be one of: {string.Join(", ", MemoryScopePolicy.ScopeTypes)}.";
            return false;
        }

        var eventType = NormalizeAllQuery(context, "eventType");
        if (!string.IsNullOrWhiteSpace(eventType) && !EventTypes.Contains(eventType))
        {
            error = $"eventType must be one of: all, {string.Join(", ", EventTypes)}.";
            return false;
        }

        var retentionClass = NormalizeAllQuery(context, "retentionClass");
        if (!string.IsNullOrWhiteSpace(retentionClass) && !MemoryRetentionClasses.All.Contains(retentionClass))
        {
            error = $"retentionClass must be one of: all, {string.Join(", ", MemoryRetentionClasses.All)}.";
            return false;
        }

        var sensitivity = NormalizeAllQuery(context, "sensitivity");
        if (!string.IsNullOrWhiteSpace(sensitivity) && !MemoryScopePolicy.Sensitivities.Contains(sensitivity))
        {
            error = $"sensitivity must be one of: all, {string.Join(", ", MemoryScopePolicy.Sensitivities)}.";
            return false;
        }

        var trustLevel = NormalizeAllQuery(context, "trustLevel");
        if (!string.IsNullOrWhiteSpace(trustLevel) && !MemoryScopePolicy.TrustLevels.Contains(trustLevel))
        {
            error = $"trustLevel must be one of: all, {string.Join(", ", MemoryScopePolicy.TrustLevels)}.";
            return false;
        }

        var redactionStatus = NormalizeAllQuery(context, "redactionStatus");
        if (!string.IsNullOrWhiteSpace(redactionStatus) && !RedactionStatuses.Contains(redactionStatus))
        {
            error = $"redactionStatus must be one of: all, {string.Join(", ", RedactionStatuses)}.";
            return false;
        }

        if (!TryReadDateTimeOffset(context, "createdFrom", out var createdFrom, out error)
            || !TryReadDateTimeOffset(context, "createdTo", out var createdTo, out error))
        {
            return false;
        }

        if (createdFrom.HasValue && createdTo.HasValue && createdFrom.Value > createdTo.Value)
        {
            error = "createdFrom must be earlier than or equal to createdTo.";
            return false;
        }

        query = new AdminSourceEventListQuery(
            principalId,
            limit,
            scopeType,
            scopeId,
            eventType,
            retentionClass,
            sensitivity,
            trustLevel,
            redactionStatus,
            createdFrom,
            createdTo,
            NormalizeOptionalQuery(context, "q"));
        return true;
    }

    private static string? NormalizeOptionalQuery(HttpContext context, string key)
    {
        var value = context.Request.Query[key].ToString().Trim();

        return value.Length == 0 ? null : value;
    }

    private static string? NormalizeAllQuery(HttpContext context, string key)
    {
        var value = NormalizeOptionalQuery(context, key);

        return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static bool TryReadDateTimeOffset(
        HttpContext context,
        string key,
        out DateTimeOffset? value,
        out string? error)
    {
        value = null;
        error = null;
        var rawValue = NormalizeOptionalQuery(context, key);

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
            rawValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            error = $"{key} must be an ISO 8601 timestamp.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static AdminMemoryFactResponse ToResponse(
        AdminMemoryFactRecord record,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        var sourceEvidence = SourceEvidenceReference.Create(
            record.SourceEventId,
            record.SourcePolicy.TrustLevel,
            record.SourcePolicy.Sensitivity);
        var sourceEvidenceLink = sourceEventLinks.BuildEvidenceLink(sourceEvidence.SourceEventId);

        return new AdminMemoryFactResponse(
            record.Id,
            record.ScopeType,
            record.ScopeId,
            record.Namespace,
            record.UserPrincipalId,
            record.ProjectId,
            record.OrgId,
            record.RoleId,
            record.AgentPrincipalId,
            record.MemoryType,
            record.Visibility,
            record.Subject,
            record.Predicate,
            record.Object,
            record.Confidence,
            record.TrustLevel,
            record.Status,
            sourceEvidence.SourceEventId,
            sourceEvidenceLink.Link,
            record.ProposedByPrincipalId,
            record.CreatedAt,
            record.UpdatedAt,
            new AdminMemoryFactPolicyResponse(
                record.ContentVisible,
                record.ContentVisibilityReason,
                record.SourcePolicy.RetentionClass,
                sourceEvidence.Sensitivity.Value,
                sourceEvidence.TrustLevel.Value,
                record.SourcePolicy.RedactionStatus,
                record.SourcePolicy.SourcePayloadIncluded));
    }

    private static AdminSourceEventResponse ToSourceEventResponse(
        AdminSourceEventRecord record,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        var sourceEvidence = SourceEvidenceReference.Create(
            record.Id,
            record.TrustLevel,
            record.Sensitivity);
        var sourceEvidenceLink = sourceEventLinks.BuildEvidenceLink(sourceEvidence.SourceEventId);
        var redactionEvidenceLink = record.RedactionEventId.HasValue
            ? sourceEventLinks.BuildEvidenceLink(record.RedactionEventId.Value)
            : null;

        return new AdminSourceEventResponse(
            sourceEvidence.SourceEventId,
            record.PrincipalId,
            record.ConversationId,
            record.AgentPrincipalId,
            record.RoleId,
            record.EventType,
            sourceEvidenceLink.Link,
            record.ContentHash,
            record.ExternalPayloadUri,
            record.RetentionClass,
            sourceEvidence.Sensitivity.Value,
            record.RedactionStatus,
            record.RedactedAt,
            record.RedactionEventId,
            redactionEvidenceLink?.Link,
            sourceEvidence.TrustLevel.Value,
            record.CreatedAt,
            new AdminSourceEventScopeResponse(
                record.ScopeType,
                record.ScopeId,
                record.ScopeOrgId,
                record.ScopeProjectId,
                record.ScopePrincipalId,
                record.ScopeRoleId),
            new AdminSourceEventPolicyResponse(
                record.SourcePayloadIncluded,
                record.ContentVisibilityReason),
            record.References.Select(reference => new AdminSourceEventReferenceResponse(
                reference.ReferenceType,
                reference.Id,
                reference.Status,
                reference.TargetType,
                reference.TargetId,
                reference.Label)).ToArray());
    }
}
