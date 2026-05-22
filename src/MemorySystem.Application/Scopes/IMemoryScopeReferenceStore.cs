namespace MemorySystem.Application.Scopes;

public interface IMemoryScopeReferenceStore
{
    Task<bool> OrganizationExistsAsync(Guid orgId, CancellationToken cancellationToken = default);

    Task<ProjectScopeReference?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> PrincipalExistsAsync(
        Guid principalId,
        string? principalType = null,
        CancellationToken cancellationToken = default);
}
