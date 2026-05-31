using DomainMemoryNamespaceParser = MemorySystem.Domain.Namespaces.MemoryNamespaceParser;

namespace MemorySystem.Application.Scopes;

public static class MemoryNamespaceParser
{
    public static bool TryParse(
        string? namespaceValue,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        if (!DomainMemoryNamespaceParser.TryParse(namespaceValue, out var parsedNamespace, out error))
        {
            memoryNamespace = null!;
            return false;
        }

        memoryNamespace = new MemoryNamespace(
            parsedNamespace!.Value,
            parsedNamespace.ScopeType,
            parsedNamespace.ScopeId,
            parsedNamespace.Segments,
            parsedNamespace.RoleId?.Value);
        return true;
    }

    public static string BuildScopePrefix(string scopeType, string scopeId)
    {
        return DomainMemoryNamespaceParser.BuildScopePrefix(scopeType, scopeId);
    }
}
