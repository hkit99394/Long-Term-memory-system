namespace MemorySystem.Api.Admin;

public sealed record AdminAuditExportRequest(
    DateTimeOffset? OccurredFrom,
    DateTimeOffset? OccurredTo,
    string ScopeType,
    string ScopeId,
    IReadOnlyList<string>? ActionTypes,
    IReadOnlyList<string>? Outcomes,
    int? Limit);
