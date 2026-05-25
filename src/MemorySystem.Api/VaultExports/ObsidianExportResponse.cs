namespace MemorySystem.Api.VaultExports;

public sealed record ObsidianExportResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ObsidianExportDocumentResponse> Documents,
    IReadOnlyList<ObsidianStaleExportDocumentResponse> StaleDocuments);

public sealed record ObsidianExportDocumentResponse(
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

public sealed record ObsidianStaleExportDocumentResponse(
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

public sealed record ObsidianArchiveExportResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ObsidianArchiveExportDocumentResponse> Documents);

public sealed record ObsidianArchiveExportDocumentResponse(
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
