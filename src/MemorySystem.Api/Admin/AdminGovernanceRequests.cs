namespace MemorySystem.Api.Admin;

public sealed class AdminGovernanceSelectionRequest
{
    public IReadOnlyList<Guid>? EventIds { get; init; }

    public string? ScopeType { get; init; }

    public string? ScopeId { get; init; }

    public string? NamespacePrefix { get; init; }

    public string? RetentionClass { get; init; }

    public string? Sensitivity { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }

    public DateTimeOffset? CreatedTo { get; init; }

    public int? MaxEvents { get; init; }

    public string? Reason { get; init; }
}

public sealed record AdminLegalHoldReleaseRequest(string? Reason);
