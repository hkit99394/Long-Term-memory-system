namespace MemorySystem.Application.Roles;

public interface IProjectRoleDefinitionStore
{
    Task<ProjectRoleDefinitionRecord> UpsertAsync(
        ProjectRoleDefinitionCommand command,
        CancellationToken cancellationToken = default);

    Task<bool> IsActiveProjectRoleAsync(
        Guid projectId,
        string roleId,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectRoleDefinitionCommand(
    Guid ActorPrincipalId,
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status);

public sealed record ProjectRoleDefinitionRecord(
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
