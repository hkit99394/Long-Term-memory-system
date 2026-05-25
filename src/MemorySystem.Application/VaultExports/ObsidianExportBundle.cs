namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianExportBundle(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ObsidianExportDocument> Documents,
    IReadOnlyList<ObsidianStaleExportDocument> StaleDocuments);
