using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianExportCandidate(
    MemoryFactRecord MemoryFact,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
