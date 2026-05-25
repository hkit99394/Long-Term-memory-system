namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianArchiveExportDocument(
    string Path,
    string Title,
    Guid MemoryFactId,
    Guid SourceEventId,
    string SourceLink,
    string MemoryType,
    string ScopeType,
    string ScopeId,
    string Namespace,
    string Status,
    string Content);
