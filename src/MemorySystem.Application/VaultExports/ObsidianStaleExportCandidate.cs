using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Application.VaultExports;

public sealed record ObsidianStaleExportCandidate(
    MemoryFactRecord MemoryFact,
    string ExportPath,
    string Reason,
    DateTimeOffset ExportedAt);
