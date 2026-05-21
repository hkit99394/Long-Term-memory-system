using System.Text.Json;

namespace MemorySystem.Api.Events;

public sealed class AppendEventRequest
{
    public Guid? PrincipalId { get; init; }

    public Guid? ConversationId { get; init; }

    public Guid? AgentPrincipalId { get; init; }

    public string? RoleId { get; init; }

    public string? EventType { get; init; }

    public string? ScopeType { get; init; }

    public string? ScopeId { get; init; }

    public Guid? ScopeOrgId { get; init; }

    public string? TrustLevel { get; init; }

    public string? RetentionClass { get; init; }

    public string? Sensitivity { get; init; }

    public string? ExternalPayloadUri { get; init; }

    public JsonElement Payload { get; init; }
}
