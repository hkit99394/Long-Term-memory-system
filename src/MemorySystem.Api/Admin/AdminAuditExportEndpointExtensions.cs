using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Api.Admin;

public static class AdminAuditExportEndpointExtensions
{
    private const int DefaultLimit = 1000;
    private const int MaxLimit = 5000;
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    private static readonly JsonSerializerOptions NdjsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static IEndpointRouteBuilder MapMemorySystemAdminAuditExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/admin/audit-exports",
            async (
                HttpContext context,
                IAdminAuditExportStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                IAccessAuditEventStore accessAuditEventStore,
                CancellationToken cancellationToken) =>
                await ExportAccessAuditAsync(context, store, accessAuthorizer, accessAuditEventStore, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        return endpoints;
    }

    private static async Task<IResult> ExportAccessAuditAsync(
        HttpContext context,
        IAdminAuditExportStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        IAccessAuditEventStore accessAuditEventStore,
        CancellationToken cancellationToken)
    {
        var body = await ApiRequestHelpers.ReadJsonBodyAsync<AdminAuditExportRequest>(
            context.Request,
            "Audit export request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return ToResult(body.Problem!);
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryCreateExportQuery(body.Value!, out var query, out var scope, out var error))
        {
            return BadRequest(error!);
        }

        var authorization = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                actorPrincipalId,
                MemoryAccessPermissions.Admin,
                scope,
                Namespace: null),
            cancellationToken);
        if (!authorization.Allowed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Audit export request is forbidden.",
                detail: authorization.Reason);
        }

        var records = await store.ListAccessAuditEventsAsync(query, cancellationToken);
        var exportId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var rowLines = records
            .Select(record => JsonSerializer.Serialize(ToExportRow(record), NdjsonOptions))
            .ToArray();
        var content = rowLines.Length == 0 ? string.Empty : string.Join('\n', rowLines) + "\n";
        var contentSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        var manifest = new AdminAuditExportManifestLine(
            "manifest",
            "memory.access-audit-export.v1",
            "ndjson",
            exportId,
            createdAt,
            new AdminAuditExportFiltersLine(
                query.OccurredFrom,
                query.OccurredTo,
                query.ScopeType,
                query.ScopeId,
                query.ActionTypes,
                query.Outcomes,
                query.Limit),
            records.Count,
            contentSha256);

        await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.AuditExport,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: actorPrincipalId,
                ScopeType: scope.ScopeType,
                ScopeId: scope.ScopeId,
                ResourceType: "audit_export",
                ResourceId: exportId.ToString("D"),
                RequestMethod: context.Request.Method,
                RequestPath: context.Request.Path.Value,
                CorrelationId: context.TraceIdentifier,
                Metadata: new Dictionary<string, string?>
                {
                    ["format"] = "ndjson",
                    ["schemaVersion"] = manifest.SchemaVersion,
                    ["rowCount"] = records.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["contentSha256"] = contentSha256,
                    ["occurredFrom"] = query.OccurredFrom.ToString("O"),
                    ["occurredTo"] = query.OccurredTo.ToString("O"),
                    ["scopeType"] = query.ScopeType,
                    ["scopeId"] = query.ScopeId
                }),
            cancellationToken);

        var manifestLine = JsonSerializer.Serialize(manifest, NdjsonOptions);
        var ndjson = manifestLine + "\n" + content;
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"access-audit-{exportId:N}.ndjson\"";

        return Results.Text(
            ndjson,
            contentType: "application/x-ndjson; charset=utf-8",
            contentEncoding: Encoding.UTF8,
            statusCode: StatusCodes.Status200OK);
    }

    private static bool TryCreateExportQuery(
        AdminAuditExportRequest request,
        out AdminAuditExportQuery query,
        out MemoryScopeResolution scope,
        out string? error)
    {
        query = null!;
        scope = null!;

        if (!request.OccurredFrom.HasValue || !request.OccurredTo.HasValue)
        {
            error = "occurredFrom and occurredTo are required.";
            return false;
        }

        if (request.OccurredFrom.Value > request.OccurredTo.Value)
        {
            error = "occurredFrom must be earlier than or equal to occurredTo.";
            return false;
        }

        if (request.OccurredTo.Value - request.OccurredFrom.Value > MaxWindow)
        {
            error = "Audit export time window must be 31 days or less.";
            return false;
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            request.ScopeType,
            request.ScopeId,
            out var scopeType,
            out var scopeId,
            out error))
        {
            return false;
        }

        scope = scopeType switch
        {
            "org" => new MemoryScopeResolution("org", scopeId, OrgId: Guid.Parse(scopeId)),
            "project" => new MemoryScopeResolution("project", scopeId, ProjectId: Guid.Parse(scopeId)),
            _ => null!
        };

        if (scope is null)
        {
            error = "Audit export currently supports org and project scopes.";
            return false;
        }

        if (!TryNormalizeAllowed(request.ActionTypes, AccessAuditActionTypes.All, out var actionTypes, out error))
        {
            return false;
        }

        if (!TryNormalizeAllowed(request.Outcomes, AccessAuditOutcomes.All, out var outcomes, out error))
        {
            return false;
        }

        var limit = request.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            error = $"limit must be between 1 and {MaxLimit}.";
            return false;
        }

        query = new AdminAuditExportQuery(
            request.OccurredFrom.Value,
            request.OccurredTo.Value,
            scope.ScopeType,
            scope.ScopeId,
            limit,
            actionTypes,
            outcomes);
        error = null;
        return true;
    }

    private static bool TryNormalizeAllowed(
        IReadOnlyList<string>? requestedValues,
        IReadOnlySet<string> allowedValues,
        out IReadOnlyList<string> values,
        out string? error)
    {
        if (requestedValues is null || requestedValues.Count == 0)
        {
            values = allowedValues.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            error = null;
            return true;
        }

        var normalized = requestedValues
            .Select(value => value?.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            values = [];
            error = "At least one filter value must be provided.";
            return false;
        }

        var unsupported = normalized.FirstOrDefault(value => !allowedValues.Contains(value));
        if (unsupported is not null)
        {
            values = [];
            error = $"Filter value '{unsupported}' is not supported.";
            return false;
        }

        values = normalized;
        error = null;
        return true;
    }

    private static AdminAuditExportEventLine ToExportRow(AdminAuditExportEventRecord record)
    {
        return new AdminAuditExportEventLine(
            "accessAuditEvent",
            record.Id,
            record.ActionType,
            record.Outcome,
            record.ActorPrincipalId,
            record.TargetPrincipalId,
            record.PrincipalType,
            record.AuthMethod,
            record.CredentialId,
            record.IdentityBindingId,
            record.ScopeType,
            record.ScopeId,
            record.RoleId,
            record.NamespacePrefix,
            record.Permission,
            record.ResourceType,
            record.ResourceId,
            record.ReasonCode,
            record.RequestMethod,
            record.RequestPath,
            record.CorrelationId,
            record.AuditMetadata,
            record.OccurredAt);
    }

    private static IResult ToResult(ApiIdempotencyResponse response)
    {
        return Results.Json(
            response.Body,
            statusCode: response.StatusCode,
            contentType: response.ContentType ?? "application/problem+json; charset=utf-8");
    }

    private static IResult BadRequest(string detail)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Audit export request is invalid.",
            detail: detail);
    }

    private sealed record AdminAuditExportManifestLine(
        string RecordType,
        string SchemaVersion,
        string Format,
        Guid ExportId,
        DateTimeOffset CreatedAt,
        AdminAuditExportFiltersLine Filters,
        int RowCount,
        string ContentSha256);

    private sealed record AdminAuditExportFiltersLine(
        DateTimeOffset OccurredFrom,
        DateTimeOffset OccurredTo,
        string ScopeType,
        string ScopeId,
        IReadOnlyList<string> ActionTypes,
        IReadOnlyList<string> Outcomes,
        int Limit);

    private sealed record AdminAuditExportEventLine(
        string RecordType,
        Guid Id,
        string ActionType,
        string Outcome,
        Guid? ActorPrincipalId,
        Guid? TargetPrincipalId,
        string? PrincipalType,
        string? AuthMethod,
        string? CredentialId,
        Guid? IdentityBindingId,
        string? ScopeType,
        string? ScopeId,
        string? RoleId,
        string? NamespacePrefix,
        string? Permission,
        string? ResourceType,
        string? ResourceId,
        string? ReasonCode,
        string? RequestMethod,
        string? RequestPath,
        string? CorrelationId,
        IReadOnlyDictionary<string, string?> AuditMetadata,
        DateTimeOffset OccurredAt);
}
