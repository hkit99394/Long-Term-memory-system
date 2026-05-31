using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.RoleMemoryLenses;

internal static class RoleMemoryLensStorageRules
{
    public static RoleMemoryLensStorageScope ResolveLensScope(MemoryScopeResolution scope)
    {
        var normalizedScope = PostgresDomainMapping.RequireScope(scope.ScopeType, scope.ScopeId);

        return normalizedScope.ScopeType switch
        {
            "global" when normalizedScope.ScopeId == "global" => new RoleMemoryLensStorageScope("global", "global"),
            "org" => new RoleMemoryLensStorageScope(
                "org",
                Require(scope.OrgId, "Organization role lens scope requires an organization id.").ToString(),
                OrgId: Require(scope.OrgId, "Organization role lens scope requires an organization id.")),
            "project" => new RoleMemoryLensStorageScope(
                "project",
                Require(scope.ProjectId, "Project role lens scope requires a project id.").ToString(),
                OrgId: Require(scope.OrgId, "Project role lens scope requires an organization id."),
                ProjectId: Require(scope.ProjectId, "Project role lens scope requires a project id.")),
            "global" => throw new InvalidOperationException("Global role lens scope requires scope id 'global'."),
            _ => throw new InvalidOperationException($"Unsupported role memory lens scope type '{normalizedScope.ScopeType}'.")
        };
    }

    public static RoleMemoryLensStorageScope ResolveLensScope(
        string scopeType,
        string scopeId,
        Guid? orgId,
        Guid? projectId)
    {
        return ResolveLensScope(new MemoryScopeResolution(
            scopeType,
            scopeId,
            OrgId: orgId,
            ProjectId: projectId));
    }

    public static RoleMemoryLensChunkScope ResolveChunkScope(
        RoleMemoryLensStorageScope lensScope,
        string roleId,
        string? requestedNamespace = null)
    {
        roleId = PostgresDomainMapping.RequireRoleId(roleId);

        var canonicalNamespace = CanonicalNamespace(lensScope, roleId);
        var namespaceValue = string.IsNullOrWhiteSpace(requestedNamespace)
            ? canonicalNamespace
            : RequireNamespaceAtOrBelow(requestedNamespace, canonicalNamespace);
        namespaceValue = PostgresDomainMapping.RequireNamespace(namespaceValue);

        return lensScope.ScopeType switch
        {
            "global" => new RoleMemoryLensChunkScope("role", roleId, namespaceValue),
            "org" => new RoleMemoryLensChunkScope(
                "org",
                Require(lensScope.OrgId, "Organization role lens chunks require an organization id.").ToString(),
                namespaceValue),
            "project" => new RoleMemoryLensChunkScope(
                "project",
                Require(lensScope.ProjectId, "Project role lens chunks require a project id.").ToString(),
                namespaceValue),
            _ => throw new InvalidOperationException($"Unsupported role memory lens scope type '{lensScope.ScopeType}'.")
        };
    }

    public static bool IsValidBaseMemoryFactScope(
        RoleMemoryLensStorageScope lensScope,
        string baseScopeType,
        Guid? baseOrgId,
        Guid? baseProjectId)
    {
        return lensScope.ScopeType switch
        {
            "global" => baseScopeType == "global",
            "org" => baseScopeType == "org"
                && baseOrgId == lensScope.OrgId,
            "project" => (baseScopeType == "project" && baseProjectId == lensScope.ProjectId)
                || (baseScopeType == "org" && baseOrgId == lensScope.OrgId),
            _ => false
        };
    }

    public static string BuildInvalidBaseScopeMessage(RoleMemoryLensStorageScope lensScope)
    {
        return lensScope.ScopeType switch
        {
            "global" => "Global role lenses must reference global memory facts.",
            "org" => "Organization role lenses must reference memory facts from the same organization.",
            "project" => "Project role lenses must reference memory facts from the target project or its organization.",
            _ => $"Unsupported role memory lens scope type '{lensScope.ScopeType}'."
        };
    }

    public static void AddScopeParameters(NpgsqlCommand command, RoleMemoryLensStorageScope lensScope)
    {
        var scope = PostgresDomainMapping.RequireScope(lensScope.ScopeType, lensScope.ScopeId);
        command.Parameters.AddWithValue("scope_type", scope.ScopeType);
        command.Parameters.AddWithValue("scope_id", scope.ScopeId);
    }

    public static void AddOwnerParameters(NpgsqlCommand command, RoleMemoryLensStorageScope lensScope)
    {
        command.Parameters.Add("org_id", NpgsqlDbType.Uuid).Value =
            lensScope.OrgId.HasValue ? lensScope.OrgId.Value : DBNull.Value;
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            lensScope.ProjectId.HasValue ? lensScope.ProjectId.Value : DBNull.Value;
    }

    private static string CanonicalNamespace(RoleMemoryLensStorageScope lensScope, string roleId)
    {
        return lensScope.ScopeType switch
        {
            "global" => $"/role/{roleId}/shared",
            "org" => $"/org/{Require(lensScope.OrgId, "Organization role lens chunks require an organization id.")}/role/{roleId}/lens",
            "project" => $"/project/{Require(lensScope.ProjectId, "Project role lens chunks require a project id.")}/role/{roleId}/lens",
            _ => throw new InvalidOperationException($"Unsupported role memory lens scope type '{lensScope.ScopeType}'.")
        };
    }

    private static string RequireNamespaceAtOrBelow(string requestedNamespace, string canonicalNamespace)
    {
        var trimmed = requestedNamespace.Trim();

        if (string.Equals(trimmed, canonicalNamespace, StringComparison.Ordinal)
            || trimmed.StartsWith(canonicalNamespace + "/", StringComparison.Ordinal))
        {
            return trimmed;
        }

        throw new InvalidOperationException(
            $"Role-lens namespace must start with '{canonicalNamespace}'.");
    }

    private static Guid Require(Guid? value, string message)
    {
        return value ?? throw new InvalidOperationException(message);
    }
}

internal sealed record RoleMemoryLensStorageScope(
    string ScopeType,
    string ScopeId,
    Guid? OrgId = null,
    Guid? ProjectId = null);

internal sealed record RoleMemoryLensChunkScope(
    string ScopeType,
    string ScopeId,
    string Namespace);
