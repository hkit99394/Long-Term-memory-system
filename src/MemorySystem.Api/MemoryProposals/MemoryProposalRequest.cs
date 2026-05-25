namespace MemorySystem.Api.MemoryProposals;

public sealed class MemoryProposalRequest
{
    public Guid? SourceEventId { get; init; }

    public string? MemoryType { get; init; }

    public string? ScopeType { get; init; }

    public string? ScopeId { get; init; }

    public string? Namespace { get; init; }

    public string? Visibility { get; init; }

    public string? Subject { get; init; }

    public string? Predicate { get; init; }

    public string? Object { get; init; }

    public decimal? Confidence { get; init; }

    public string? TrustLevel { get; init; }

    public string? Sensitivity { get; init; }

    public string? RoleId { get; init; }

    public Guid? BaseMemoryFactId { get; init; }
}
