using System.Security.Cryptography;
using System.Text;
using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Events;

public sealed class EventAppendWorkflow(
    IEventStore eventStore,
    IMemoryScopeResolver scopeResolver,
    IMemoryAccessAuthorizer accessAuthorizer) : IEventAppendWorkflow
{
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

    private static readonly IReadOnlySet<string> RetentionClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "ephemeral",
        "standard",
        "audit",
        "legal_hold",
        "erasure_requested"
    };

    public async Task<EventAppendWorkflowResult> AppendAsync(
        EventAppendWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestHash);

        var scopeResult = await scopeResolver.ResolveEventScopeAsync(
            new MemoryEventScopeRequest(
                request.AuthenticatedPrincipalId,
                request.ScopeType,
                request.ScopeId,
                request.ScopeOrgId,
                request.ConversationId,
                request.AgentPrincipalId,
                request.RoleId),
            cancellationToken);

        if (!scopeResult.Succeeded)
        {
            return EventAppendWorkflowResult.InvalidScope(scopeResult.Error!);
        }

        var accessDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                request.AuthenticatedPrincipalId,
                MemoryAccessPermissions.Write,
                scopeResult.Resolution!,
                EventAuthorizationNamespace(scopeResult.Resolution!)),
            cancellationToken);

        if (!accessDecision.Allowed)
        {
            return EventAppendWorkflowResult.Forbidden(accessDecision.Reason!);
        }

        if (!TryMap(request, scopeResult.Resolution!, out var command, out var failure))
        {
            return failure!;
        }

        try
        {
            var result = await eventStore.AppendAsync(
                command,
                request.IdempotencyRecordId,
                request.RequestHash,
                cancellationToken);

            return EventAppendWorkflowResult.Stored(result);
        }
        catch (EventScopeNotFoundException exception)
        {
            return EventAppendWorkflowResult.InvalidScope(exception.Message);
        }
    }

    private static bool TryMap(
        EventAppendWorkflowRequest request,
        MemoryScopeResolution resolvedScope,
        out AppendEventCommand command,
        out EventAppendWorkflowResult? failure)
    {
        command = null!;
        failure = null;

        if (request.PrincipalId.HasValue && request.PrincipalId.Value != request.AuthenticatedPrincipalId)
        {
            failure = EventAppendWorkflowResult.InvalidRequest("principalId must match the authenticated principal.");
            return false;
        }

        if (!TryNormalizeRequired(request.EventType, EventTypes, "eventType", out var eventType, out var error)
            || !TryNormalizeOptional(request.TrustLevel, MemoryScopePolicy.TrustLevels, "trustLevel", "user_scoped", out var trustLevel, out error)
            || !TryNormalizeOptional(request.RetentionClass, RetentionClasses, "retentionClass", "standard", out var retentionClass, out error)
            || !TryNormalizeOptional(request.Sensitivity, MemoryScopePolicy.Sensitivities, "sensitivity", "none", out var sensitivity, out error))
        {
            failure = EventAppendWorkflowResult.InvalidRequest(error!);
            return false;
        }

        if (!MemoryTrustPolicy.IsExternallyAccepted(trustLevel))
        {
            failure = EventAppendWorkflowResult.InvalidRequest("trustLevel requires a trusted internal source.");
            return false;
        }

        if (request.Payload.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            failure = EventAppendWorkflowResult.InvalidRequest("payload must be a JSON object.");
            return false;
        }

        var contentJson = request.Payload.GetRawText();
        var contentHash = ComputeSha256(contentJson);

        command = new AppendEventCommand(
            request.AuthenticatedPrincipalId,
            resolvedScope.ConversationId,
            resolvedScope.AgentPrincipalId,
            resolvedScope.RoleId,
            eventType,
            contentJson,
            contentHash,
            string.IsNullOrWhiteSpace(request.ExternalPayloadUri) ? null : request.ExternalPayloadUri.Trim(),
            retentionClass,
            sensitivity,
            trustLevel,
            new EventScope(
                resolvedScope.ScopeType,
                resolvedScope.ScopeId,
                resolvedScope.OrgId,
                resolvedScope.ProjectId,
                resolvedScope.PrincipalId,
                resolvedScope.ScopeRoleId));
        return true;
    }

    private static bool TryNormalizeRequired(
        string? value,
        IReadOnlySet<string> allowedValues,
        string fieldName,
        out string normalizedValue,
        out string? error)
    {
        normalizedValue = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{fieldName} is required.";
            return false;
        }

        normalizedValue = value.Trim().ToLowerInvariant();

        if (!allowedValues.Contains(normalizedValue))
        {
            error = $"{fieldName} is not supported.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeOptional(
        string? value,
        IReadOnlySet<string> allowedValues,
        string fieldName,
        string defaultValue,
        out string normalizedValue,
        out string? error)
    {
        normalizedValue = string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();
        error = null;

        if (!allowedValues.Contains(normalizedValue))
        {
            error = $"{fieldName} is not supported.";
            return false;
        }

        return true;
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? EventAuthorizationNamespace(MemoryScopeResolution scope)
    {
        return scope.ScopeType is "global" or "session"
            ? MemoryNamespaceParser.BuildScopePrefix(scope.ScopeType, scope.ScopeId) + "events"
            : null;
    }
}
