using MemorySystem.Application.Roles;
using MemorySystem.Domain.Roles;

namespace MemorySystem.IntegrationTests;

internal sealed class TestProjectRoleDefinitionStore(params string[] activeProjectRoleIds) : IProjectRoleDefinitionStore
{
    private readonly HashSet<string> activeProjectRoleIds = activeProjectRoleIds
        .Select(roleId =>
        {
            if (!MemoryRoleId.TryNormalizeIdentifier(roleId, out var normalizedRoleId, out var error))
            {
                throw new ArgumentException(error, nameof(activeProjectRoleIds));
            }

            return normalizedRoleId;
        })
        .ToHashSet(StringComparer.Ordinal);

    public Task<ProjectRoleDefinitionRecord> UpsertAsync(
        ProjectRoleDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> IsActiveProjectRoleAsync(
        Guid projectId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            MemoryRoleId.IsDefaultTemplate(roleId)
            || (MemoryRoleId.TryNormalizeIdentifier(roleId, out var normalizedRoleId, out _)
                && activeProjectRoleIds.Contains(normalizedRoleId)));
    }
}
