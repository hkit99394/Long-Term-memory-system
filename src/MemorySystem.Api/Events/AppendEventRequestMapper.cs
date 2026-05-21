using System.Security.Cryptography;
using System.Text;
using MemorySystem.Infrastructure.Events;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Events;

internal static class AppendEventRequestMapper
{
    public static bool TryMap(
        Guid authenticatedPrincipalId,
        AppendEventRequest request,
        out AppendEventCommand command,
        out ProblemDetails? problem)
    {
        command = null!;
        problem = null;

        if (request.PrincipalId.HasValue && request.PrincipalId.Value != authenticatedPrincipalId)
        {
            problem = BadRequest("principalId must match the authenticated API principal.");
            return false;
        }

        if (!TryNormalizeRequired(request.EventType, ApiEventConstants.EventTypes, "eventType", out var eventType, out problem))
        {
            return false;
        }

        if (!TryNormalizeOptional(request.TrustLevel, ApiEventConstants.TrustLevels, "trustLevel", "user_scoped", out var trustLevel, out problem)
            || !TryNormalizeOptional(request.RetentionClass, ApiEventConstants.RetentionClasses, "retentionClass", "standard", out var retentionClass, out problem)
            || !TryNormalizeOptional(request.Sensitivity, ApiEventConstants.Sensitivities, "sensitivity", "none", out var sensitivity, out problem))
        {
            return false;
        }

        if (request.Payload.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            problem = BadRequest("payload must be a JSON object.");
            return false;
        }

        var contentJson = request.Payload.GetRawText();
        var contentHash = ComputeSha256(contentJson);

        if (!TryMapScope(authenticatedPrincipalId, request, out var scope, out var conversationId, out var agentPrincipalId, out var roleId, out problem))
        {
            return false;
        }

        command = new AppendEventCommand(
            authenticatedPrincipalId,
            conversationId,
            agentPrincipalId,
            roleId,
            eventType,
            contentJson,
            contentHash,
            string.IsNullOrWhiteSpace(request.ExternalPayloadUri) ? null : request.ExternalPayloadUri.Trim(),
            retentionClass,
            sensitivity,
            trustLevel,
            scope);
        return true;
    }

    private static bool TryMapScope(
        Guid authenticatedPrincipalId,
        AppendEventRequest request,
        out EventScope scope,
        out Guid? conversationId,
        out Guid? agentPrincipalId,
        out string? roleId,
        out ProblemDetails? problem)
    {
        scope = null!;
        conversationId = request.ConversationId;
        agentPrincipalId = request.AgentPrincipalId;
        roleId = NormalizeOptionalRoleId(request.RoleId);
        problem = null;

        if (!TryNormalizeRequired(request.ScopeType, ApiEventConstants.ScopeTypes, "scopeType", out var scopeType, out problem))
        {
            return false;
        }

        if (roleId is not null && !ApiEventConstants.RoleIds.Contains(roleId))
        {
            problem = BadRequest("roleId is not supported.");
            return false;
        }

        var scopeId = request.ScopeId?.Trim();

        switch (scopeType)
        {
            case "global":
                if (!string.IsNullOrWhiteSpace(scopeId) && !string.Equals(scopeId, "global", StringComparison.Ordinal))
                {
                    problem = BadRequest("scopeId must be 'global' for global scope.");
                    return false;
                }

                scope = new EventScope("global", "global", null, null, null, null);
                return true;

            case "org":
                if (!TryParseRequiredGuid(scopeId, "scopeId", out var orgId, out problem))
                {
                    return false;
                }

                scope = new EventScope("org", orgId.ToString(), orgId, null, null, null);
                return true;

            case "project":
                if (!TryParseRequiredGuid(scopeId, "scopeId", out var projectId, out problem))
                {
                    return false;
                }

                scope = new EventScope("project", projectId.ToString(), request.ScopeOrgId, projectId, null, null);
                return true;

            case "user":
                if (!TryParseRequiredGuid(scopeId, "scopeId", out var userPrincipalId, out problem))
                {
                    return false;
                }

                if (userPrincipalId != authenticatedPrincipalId)
                {
                    problem = BadRequest("User-scoped events must use the authenticated principal as scopeId.");
                    return false;
                }

                scope = new EventScope("user", userPrincipalId.ToString(), null, null, userPrincipalId, null);
                return true;

            case "agent":
                if (!TryParseRequiredGuid(scopeId, "scopeId", out var scopedAgentPrincipalId, out problem))
                {
                    return false;
                }

                if (agentPrincipalId.HasValue && agentPrincipalId.Value != scopedAgentPrincipalId)
                {
                    problem = BadRequest("agentPrincipalId must match scopeId for agent-scoped events.");
                    return false;
                }

                agentPrincipalId = scopedAgentPrincipalId;
                scope = new EventScope("agent", scopedAgentPrincipalId.ToString(), null, null, scopedAgentPrincipalId, null);
                return true;

            case "role":
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    problem = BadRequest("scopeId is required.");
                    return false;
                }

                var scopedRoleId = scopeId.ToLowerInvariant();

                if (!ApiEventConstants.RoleIds.Contains(scopedRoleId))
                {
                    problem = BadRequest("scopeId is not a supported role.");
                    return false;
                }

                if (roleId is not null && !string.Equals(roleId, scopedRoleId, StringComparison.Ordinal))
                {
                    problem = BadRequest("roleId must match scopeId for role-scoped events.");
                    return false;
                }

                roleId = scopedRoleId;
                scope = new EventScope("role", scopedRoleId, null, null, null, scopedRoleId);
                return true;

            case "session":
                if (string.IsNullOrWhiteSpace(scopeId) || string.Equals(scopeId, "global", StringComparison.Ordinal))
                {
                    problem = BadRequest("scopeId is required for session scope and must not be 'global'.");
                    return false;
                }

                if (Guid.TryParse(scopeId, out var parsedConversationId))
                {
                    if (conversationId.HasValue && conversationId.Value != parsedConversationId)
                    {
                        problem = BadRequest("conversationId must match scopeId for GUID session scopes.");
                        return false;
                    }

                    conversationId = parsedConversationId;
                }

                scope = new EventScope("session", scopeId, null, null, null, null);
                return true;

            default:
                problem = BadRequest("scopeType is not supported.");
                return false;
        }
    }

    private static bool TryNormalizeRequired(
        string? value,
        IReadOnlySet<string> allowedValues,
        string fieldName,
        out string normalizedValue,
        out ProblemDetails? problem)
    {
        normalizedValue = string.Empty;
        problem = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            problem = BadRequest($"{fieldName} is required.");
            return false;
        }

        normalizedValue = value.Trim().ToLowerInvariant();

        if (!allowedValues.Contains(normalizedValue))
        {
            problem = BadRequest($"{fieldName} is not supported.");
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
        out ProblemDetails? problem)
    {
        normalizedValue = string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();
        problem = null;

        if (!allowedValues.Contains(normalizedValue))
        {
            problem = BadRequest($"{fieldName} is not supported.");
            return false;
        }

        return true;
    }

    private static bool TryParseRequiredGuid(
        string? value,
        string fieldName,
        out Guid guid,
        out ProblemDetails? problem)
    {
        problem = null;

        if (Guid.TryParse(value, out guid))
        {
            return true;
        }

        problem = BadRequest($"{fieldName} must be a valid GUID.");
        return false;
    }

    private static string? NormalizeOptionalRoleId(string? roleId)
    {
        return string.IsNullOrWhiteSpace(roleId) ? null : roleId.Trim().ToLowerInvariant();
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ProblemDetails BadRequest(string detail)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Event request is invalid.",
            Detail = detail
        };
    }
}
