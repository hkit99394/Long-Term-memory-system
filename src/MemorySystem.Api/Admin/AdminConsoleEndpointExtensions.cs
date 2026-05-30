using MemorySystem.Api.Http;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Api.Admin;

public static class AdminConsoleEndpointExtensions
{
    private const int MaxMemoryFactLimit = 100;

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

    private static string? NormalizeOptionalQuery(HttpContext context, string key)
    {
        var value = context.Request.Query[key].ToString().Trim();

        return value.Length == 0 ? null : value;
    }

    private static AdminMemoryFactResponse ToResponse(
        AdminMemoryFactRecord record,
        ISourceEventLinkBuilder sourceEventLinks)
    {
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
            record.SourceEventId,
            sourceEventLinks.Build(record.SourceEventId),
            record.ProposedByPrincipalId,
            record.CreatedAt,
            record.UpdatedAt,
            new AdminMemoryFactPolicyResponse(
                record.ContentVisible,
                record.ContentVisibilityReason,
                record.SourcePolicy.RetentionClass,
                record.SourcePolicy.Sensitivity,
                record.SourcePolicy.TrustLevel,
                record.SourcePolicy.RedactionStatus,
                record.SourcePolicy.SourcePayloadIncluded));
    }
}
