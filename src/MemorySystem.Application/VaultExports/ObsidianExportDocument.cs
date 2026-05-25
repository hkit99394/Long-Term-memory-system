namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianExportDocument(
    string Path,
    string Title,
    string MemoryType,
    string ScopeType,
    string ScopeId,
    string Namespace,
    Guid MemoryFactId,
    Guid SourceEventId,
    string SourceLink,
    string Content);
