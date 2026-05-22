using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Access;

public interface IMemoryAccessReferenceStore
{
    Task<string?> FindOrganizationAccessLevelAsync(
        Guid principalId,
        Guid orgId,
        CancellationToken cancellationToken = default);

    Task<string?> FindProjectAccessLevelAsync(
        Guid principalId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> HasRoleAssignmentAsync(
        Guid principalId,
        string roleId,
        MemoryScopeResolution scope,
        CancellationToken cancellationToken = default);

    Task<bool> HasNamespaceGrantAsync(
        Guid principalId,
        MemoryScopeResolution scope,
        string permission,
        string namespaceValue,
        CancellationToken cancellationToken = default);
}
