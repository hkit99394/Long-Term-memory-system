using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactWriteCommand(
    MemoryScopeResolution Scope,
    string Namespace,
    string MemoryType,
    string Visibility,
    string Subject,
    string Predicate,
    string Object,
    decimal Confidence,
    Guid SourceEventId,
    Guid ProposedByPrincipalId,
    string Status = MemoryFactStatuses.Active,
    Guid? Id = null);
