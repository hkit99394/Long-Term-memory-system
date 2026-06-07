using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Evidence;
using MemorySystem.Domain.Lifecycle;
using MemorySystem.Domain.Namespaces;
using MemorySystem.Domain.Retention;
using MemorySystem.Domain.Retrieval;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;
using MemorySystem.Domain.Sensitivity;
using MemorySystem.Domain.Trust;
using DomainNamespaceParser = MemorySystem.Domain.Namespaces.MemoryNamespaceParser;

namespace MemorySystem.Infrastructure.DomainMapping;

internal static class PostgresDomainMapping
{
    public static MemoryScope RequireScope(
        string? scopeType,
        string? scopeId,
        string fieldName = "scope")
    {
        return Require(
            () => MemoryScope.TryNormalize(scopeType, scopeId, out var scope, out var error)
                ? (scope, error)
                : (null, error),
            fieldName);
    }

    public static string RequireScopeType(string? scopeType, string fieldName = "scopeType")
    {
        return Require(
            () => MemoryScopeType.TryNormalize(scopeType, out var normalizedScopeType, out var error)
                ? (normalizedScopeType, error)
                : (null, error),
            fieldName).Value;
    }

    public static MemoryScopeResolution RequireScopeResolution(
        string? scopeType,
        string? scopeId,
        Guid? orgId = null,
        Guid? projectId = null,
        Guid? principalId = null,
        string? roleId = null,
        string? scopeRoleId = null,
        Guid? conversationId = null,
        Guid? agentPrincipalId = null)
    {
        var scope = RequireScope(scopeType, scopeId);
        var normalizedRoleId = NormalizeOptionalRoleId(roleId);
        var normalizedScopeRoleId = NormalizeOptionalRoleId(scopeRoleId);

        return new MemoryScopeResolution(
            scope.ScopeType,
            scope.ScopeId,
            OrgId: orgId,
            ProjectId: projectId,
            PrincipalId: scope.ScopeType == MemoryScopeType.Agent ? agentPrincipalId : principalId,
            RoleId: normalizedRoleId,
            ScopeRoleId: normalizedScopeRoleId,
            ConversationId: conversationId,
            AgentPrincipalId: agentPrincipalId);
    }

    public static string RequireNamespace(string? namespaceValue, string fieldName = "namespace")
    {
        return Require(
            () => DomainNamespaceParser.TryParse(namespaceValue, out var memoryNamespace, out var error)
                ? (memoryNamespace, error)
                : (null, error),
            fieldName).Value;
    }

    public static string? NormalizeOptionalRoleId(string? roleId, string fieldName = "roleId")
    {
        return string.IsNullOrWhiteSpace(roleId)
            ? null
            : RequireRoleId(roleId, fieldName);
    }

    public static string RequireRoleId(string? roleId, string fieldName = "roleId")
    {
        if (MemoryRoleId.TryNormalizeIdentifier(roleId, out var normalizedRoleId, out var error))
        {
            return normalizedRoleId;
        }

        throw new InvalidOperationException(
            $"Database value for {fieldName} is invalid: {error ?? "unknown error"}");
    }

    public static string RequireLifecycleStatus(string? status, string fieldName = "status")
    {
        return Require(
            () => MemoryLifecycleStatus.TryParse(status, out var lifecycleStatus, out var error)
                ? (lifecycleStatus, error)
                : (null, error),
            fieldName).Value;
    }

    public static string RequireRetentionClass(string? retentionClass, string fieldName = "retentionClass")
    {
        return Require(
            () => MemoryRetentionClass.TryNormalize(retentionClass, out var normalizedRetentionClass, out var error)
                ? (normalizedRetentionClass, error)
                : (null, error),
            fieldName).Value;
    }

    public static string RequireSensitivity(string? sensitivity, string fieldName = "sensitivity")
    {
        return Require(
            () => MemorySensitivity.TryNormalize(sensitivity, out var normalizedSensitivity, out var error)
                ? (normalizedSensitivity, error)
                : (null, error),
            fieldName).Value;
    }

    public static string RequireTrustLevel(string? trustLevel, string fieldName = "trustLevel")
    {
        return Require(
            () => MemoryTrustLevel.TryNormalize(trustLevel, out var normalizedTrustLevel, out var error)
                ? (normalizedTrustLevel, error)
                : (null, error),
            fieldName).Value;
    }

    public static SourceEvidenceReference RequireSourceEvidence(
        Guid sourceEventId,
        string? trustLevel,
        string? sensitivity,
        string fieldName = "sourceEvidence")
    {
        return Require(
            () => SourceEvidenceReference.TryCreate(sourceEventId, trustLevel, sensitivity, out var sourceEvidence, out var error)
                ? (sourceEvidence, error)
                : (null, error),
            fieldName);
    }

    public static string RequireFeedbackType(string? feedbackType, string fieldName = "feedbackType")
    {
        return Require(
            () => MemoryRetrievalFeedbackType.TryNormalize(feedbackType, out var normalizedFeedbackType, out var error)
                ? (normalizedFeedbackType, error)
                : (null, error),
            fieldName).Value;
    }

    public static string? NormalizeOptionalFeedbackType(string? feedbackType)
    {
        return string.IsNullOrWhiteSpace(feedbackType)
            ? null
            : RequireFeedbackType(feedbackType);
    }

    private static T Require<T>(
        Func<(T? Value, string? Error)> factory,
        string fieldName)
        where T : class
    {
        var (value, error) = factory();

        return value
            ?? throw new InvalidOperationException(
                $"Database value for {fieldName} is invalid: {error ?? "unknown error"}");
    }
}
