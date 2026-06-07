using MemorySystem.Api.Http;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Retention;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Evidence;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using System.Text.Json;

namespace MemorySystem.Api.Admin;

public static class AdminConsoleEndpointExtensions
{
    private const int MaxMemoryFactLimit = 100;
    private const int MaxSourceEventLimit = 100;
    private const int ComplianceLegalHoldLimit = 100;
    private const int ComplianceRetentionReportLimit = 200;
    private static readonly string PilotReadinessStatusRelativePath = Path.Combine(
        "docs",
        "external-pilot-readiness-status.json");

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
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/source-events",
            async (
                HttpContext context,
                IAdminMemoryInspectionStore store,
                ISourceEventLinkBuilder sourceEventLinks,
                CancellationToken cancellationToken) =>
                await ListSourceEventsAsync(context, store, sourceEventLinks, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/compliance/status",
            async (
                HttpContext context,
                IAdminGovernanceStore governanceStore,
                CancellationToken cancellationToken) =>
                await ReadComplianceStatusAsync(context, governanceStore, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/pilot/readiness",
            async (
                HttpContext context,
                IWebHostEnvironment environment,
                CancellationToken cancellationToken) =>
                await ReadPilotReadinessAsync(context, environment, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

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

    private static async Task<IResult> ReadComplianceStatusAsync(
        HttpContext context,
        IAdminGovernanceStore governanceStore,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        var activeLegalHolds = await governanceStore.ListLegalHoldsAsync(
            new AdminLegalHoldListQuery(principalId, ComplianceLegalHoldLimit, "active"),
            cancellationToken);
        var retentionRows = await governanceStore.ReadRetentionReportAsync(
            new AdminRetentionReportQuery(principalId, ComplianceRetentionReportLimit),
            cancellationToken);

        var items = new[]
        {
            RetentionReportItem(retentionRows.Count),
            LegalHoldItem(activeLegalHolds.Sum(hold => hold.ActiveEventCount), activeLegalHolds.Count),
            ErasureReplayItem(),
            PermissionDriftItem(),
            ComplianceEvidencePackageItem()
        };

        return Results.Ok(new AdminComplianceStatusResponse(
            DateTimeOffset.UtcNow,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false,
            items));
    }

    private static async Task<IResult> ReadPilotReadinessAsync(
        HttpContext context,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out _, out var principalFailure))
        {
            return principalFailure;
        }

        var statusPath = TryFindPilotReadinessStatusPath(environment);
        if (statusPath is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Admin pilot readiness status is unavailable.",
                detail: $"{PilotReadinessStatusRelativePath} could not be found.");
        }

        try
        {
            await using var stream = File.OpenRead(statusPath);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            return Results.Json(document.RootElement.Clone());
        }
        catch (JsonException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Admin pilot readiness status is invalid.",
                detail: exception.Message);
        }
    }

    private static string? TryFindPilotReadinessStatusPath(IWebHostEnvironment environment)
    {
        foreach (var root in EnumerateCandidateRoots(environment))
        {
            var candidate = Path.Combine(root, PilotReadinessStatusRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateRoots(IWebHostEnvironment environment)
    {
        foreach (var root in new[] { environment.ContentRootPath, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);

            while (directory is not null)
            {
                yield return directory.FullName;
                directory = directory.Parent;
            }
        }
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
            NormalizeRoleIdQuery(context, out var roleError),
            namespacePrefix,
            NormalizeOptionalQuery(context, "q"));
        if (roleError is not null)
        {
            error = roleError;
            return false;
        }

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
            NormalizeRoleIdQuery(context, out var roleError),
            createdFrom,
            createdTo,
            NormalizeOptionalQuery(context, "q"));
        if (roleError is not null)
        {
            error = roleError;
            return false;
        }

        return true;
    }

    private static string? NormalizeRoleIdQuery(HttpContext context, out string? error)
    {
        error = null;
        var value = NormalizeOptionalQuery(context, "roleId");
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (MemoryScopePolicy.TryNormalizeRoleIdentifier(value, out var normalizedRoleId, out var roleError))
        {
            return normalizedRoleId;
        }

        error = roleError;
        return null;
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

    private static AdminComplianceStatusItemResponse RetentionReportItem(int visibleRowCount)
    {
        return new AdminComplianceStatusItemResponse(
            "retention_report",
            "Retention Report",
            visibleRowCount > 0 ? "available" : "empty",
            "retention_report",
            visibleRowCount > 0
                ? $"{visibleRowCount} authorized retention groups are visible to the operator."
                : "No authorized retention groups are visible to the operator.",
            visibleRowCount,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false,
            Links:
            [
                Link("Retention report API", "/api/admin/governance/retention-report", "api", "GET"),
                Link("Retention policy", "docs/retention-policy.md", "documentation", "DOC"),
                Link("Retention minimization job", "scripts/platform-retention-minimization.sh", "script", "SCRIPT")
            ],
            Metrics:
            [
                Metric("memorysystem_retention_minimization_success", "Latest standard/audit minimization job outcome."),
                Metric("memorysystem_retention_minimization_candidates", "Candidate source-event rows considered by minimization.")
            ]);
    }

    private static AdminComplianceStatusItemResponse LegalHoldItem(int activeEventCount, int visibleHoldCount)
    {
        return new AdminComplianceStatusItemResponse(
            "legal_hold",
            "Legal Hold",
            activeEventCount > 0 ? "active" : "clear",
            "legal_hold_summary",
            activeEventCount > 0
                ? $"{activeEventCount} held events are visible across {visibleHoldCount} active holds."
                : "No active held events are visible to the operator.",
            activeEventCount,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false,
            Links:
            [
                Link("Legal holds API", "/api/admin/governance/legal-holds?status=active", "api", "GET"),
                Link("Create legal hold API", "/api/admin/governance/legal-holds", "api", "POST"),
                Link("Retention policy legal hold rules", "docs/retention-policy.md", "documentation", "DOC")
            ],
            Metrics: []);
    }

    private static AdminComplianceStatusItemResponse ErasureReplayItem()
    {
        return new AdminComplianceStatusItemResponse(
            "erasure_replay",
            "Erasure Replay",
            "configured",
            "erasure_replay",
            "Restore validation imports the payload-safe redaction ledger and verifies post-backup erasure actions before promotion.",
            null,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false,
            Links:
            [
                Link("Erasure execution API", "/api/admin/governance/erasures", "api", "POST"),
                Link("Erasure replay ledger", "scripts/platform-erasure-replay-ledger-export.sh", "script", "SCRIPT"),
                Link("Restore validation job", "scripts/platform-restore-validation.sh", "script", "SCRIPT"),
                Link("GC-03 replay contract", "docs/backup-erasure-replay-validation-gc03.md", "documentation", "DOC")
            ],
            Metrics:
            [
                Metric("memorysystem_restore_erasure_replay_validation_success", "Latest restore-time erasure replay validation outcome."),
                Metric("memorysystem_restore_erasure_replay_failures", "Rows that still exposed erased projections after replay validation.")
            ]);
    }

    private static AdminComplianceStatusItemResponse PermissionDriftItem()
    {
        return new AdminComplianceStatusItemResponse(
            "permission_drift",
            "Permission Drift",
            "available",
            "permission_drift_report",
            "The permission-drift report compares local access records and effective-access previews without exposing memory payloads.",
            null,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false,
            Links:
            [
                Link("Permission drift API", "/api/admin/access/permission-drift", "api", "POST"),
                Link("GC-02 drift contract", "docs/permission-drift-report-gc02.md", "documentation", "DOC")
            ],
            Metrics: []);
    }

    private static AdminComplianceStatusItemResponse ComplianceEvidencePackageItem()
    {
        return new AdminComplianceStatusItemResponse(
            "compliance_evidence_package",
            "Evidence Package",
            "configured",
            "compliance_evidence_package",
            "The package command links audit export, retention, legal hold, drift, replay, backup, release, benchmark, and alert-route evidence by ids, counts, and hashes.",
            null,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false,
            Links:
            [
                Link("Evidence package job", "scripts/platform-compliance-evidence-package.sh", "script", "SCRIPT"),
                Link("GC-06 evidence contract", "docs/compliance-evidence-package-gc06.md", "documentation", "DOC"),
                Link("Audit export API", "/api/admin/audit-exports", "api", "POST")
            ],
            Metrics:
            [
                Metric("memorysystem_compliance_evidence_package_success", "Latest compliance package generation outcome."),
                Metric("memorysystem_compliance_evidence_package_missing_required_artifacts", "Required evidence artifacts missing from the latest package.")
            ]);
    }

    private static AdminComplianceStatusLinkResponse Link(
        string label,
        string href,
        string kind,
        string method)
    {
        return new AdminComplianceStatusLinkResponse(label, href, kind, method);
    }

    private static AdminComplianceStatusMetricResponse Metric(
        string name,
        string description)
    {
        return new AdminComplianceStatusMetricResponse(name, description);
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
