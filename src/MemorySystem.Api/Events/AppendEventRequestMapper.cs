using System.Security.Cryptography;
using System.Text;
using MemorySystem.Application.Scopes;
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
        roleId = null;
        problem = null;

        if (!MemoryScopePolicy.TryNormalizeEventScope(
            authenticatedPrincipalId,
            request.ScopeType,
            request.ScopeId,
            request.ScopeOrgId,
            request.ConversationId,
            request.AgentPrincipalId,
            request.RoleId,
            out var resolvedScope,
            out var error))
        {
            problem = BadRequest(error!);
            return false;
        }

        conversationId = resolvedScope.ConversationId;
        agentPrincipalId = resolvedScope.AgentPrincipalId;
        roleId = resolvedScope.RoleId;
        scope = new EventScope(
            resolvedScope.ScopeType,
            resolvedScope.ScopeId,
            resolvedScope.OrgId,
            resolvedScope.ProjectId,
            resolvedScope.PrincipalId,
            resolvedScope.ScopeRoleId);
        return true;
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
