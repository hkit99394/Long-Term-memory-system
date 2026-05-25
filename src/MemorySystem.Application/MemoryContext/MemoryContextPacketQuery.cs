namespace MemorySystem.Application.MemoryContext;

public sealed record MemoryContextPacketQuery(
    Guid PrincipalId,
    string Query,
    int Limit = 10,
    string? TargetScopeType = null,
    string? TargetScopeId = null,
    string? RoleId = null);
