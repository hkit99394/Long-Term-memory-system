using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactSubjectPredicateQuery(
    MemoryScopeResolution Scope,
    string MemoryType,
    string Subject,
    string Predicate);
