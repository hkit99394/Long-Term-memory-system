namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianExportQuery(
    Guid PrincipalId,
    string? ScopeType,
    string? ScopeId,
    int Limit);
