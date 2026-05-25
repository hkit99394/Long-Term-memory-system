using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.RoleMemoryLenses;

public sealed record RoleMemoryLensWriteCommand(
    MemoryScopeResolution Scope,
    string RoleId,
    Guid BaseMemoryFactId,
    string Interpretation,
    decimal Confidence,
    Guid SourceEventId,
    Guid ProposedByPrincipalId,
    string Status = MemoryFactStatuses.Active,
    Guid? Id = null);
