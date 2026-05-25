namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianStaleExportDocument(
    string Path,
    Guid MemoryFactId,
    Guid SourceEventId,
    string SourceLink,
    string MemoryType,
    string ScopeType,
    string ScopeId,
    string Namespace,
    string Status,
    string Reason,
    string Content);
