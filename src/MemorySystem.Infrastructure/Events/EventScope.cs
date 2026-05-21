namespace MemorySystem.Infrastructure.Events;

public sealed record EventScope(
    string Type,
    string Id,
    Guid? OrgId,
    Guid? ProjectId,
    Guid? PrincipalId,
    string? RoleId);
