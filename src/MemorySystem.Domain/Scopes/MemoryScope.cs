namespace MemorySystem.Domain.Scopes;

public sealed record MemoryScope(
    MemoryScopeType Type,
    MemoryScopeId Id)
{
    public string ScopeType => Type.Value;

    public string ScopeId => Id.Value;

    public static bool TryNormalize(
        string? scopeType,
        string? scopeId,
        out MemoryScope? scope,
        out string? error)
    {
        scope = null;

        if (!MemoryScopeType.TryNormalize(scopeType, out var normalizedScopeType, out error))
        {
            return false;
        }

        if (!MemoryScopeId.TryNormalize(normalizedScopeType!, scopeId, out var normalizedScopeId, out error))
        {
            return false;
        }

        scope = new MemoryScope(normalizedScopeType!, normalizedScopeId!);
        return true;
    }

    internal static MemoryScope FromNormalized(string scopeType, string scopeId)
    {
        return new MemoryScope(
            MemoryScopeType.FromNormalized(scopeType),
            MemoryScopeId.FromNormalized(scopeId));
    }

    public override string ToString()
    {
        return $"{ScopeType}:{ScopeId}";
    }
}
