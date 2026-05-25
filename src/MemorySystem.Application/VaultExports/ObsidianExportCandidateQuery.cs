namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianExportCandidateQuery(
    string? ScopeType,
    string? ScopeId,
    int Limit);
