namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianArchiveExportBundle(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ObsidianArchiveExportDocument> Documents);
