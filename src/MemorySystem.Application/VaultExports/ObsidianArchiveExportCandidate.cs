using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianArchiveExportCandidate(
    MemoryFactRecord MemoryFact,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
