using MemorySystem.Application.MemoryProposals;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.MemoryProposals;

internal static class MemoryProposalRequestMapper
{
    public static bool TryMap(
        MemoryProposalRequest request,
        bool sourceEventExists,
        out MemoryProposalCommand command,
        out ProblemDetails? problem)
    {
        command = null!;
        problem = null;

        var memoryType = Normalize(request.MemoryType);
        var scopeType = Normalize(request.ScopeType);
        var scopeId = request.ScopeId?.Trim() ?? string.Empty;
        var namespaceValue = request.Namespace?.Trim() ?? string.Empty;
        var visibility = Normalize(request.Visibility, "private");
        var trustLevel = Normalize(request.TrustLevel, "user_scoped");
        var sensitivity = Normalize(request.Sensitivity, "none");

        if (!IsSupportedScope(scopeType))
        {
            problem = BadRequest("scopeType is required and must be a supported scope.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            problem = BadRequest("scopeId is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(namespaceValue) || !namespaceValue.StartsWith("/", StringComparison.Ordinal))
        {
            problem = BadRequest("namespace is required and must start with '/'.");
            return false;
        }

        if (request.Confidence is < 0 or > 1)
        {
            problem = BadRequest("confidence must be between 0 and 1 when provided.");
            return false;
        }

        command = new MemoryProposalCommand(
            request.SourceEventId,
            sourceEventExists,
            memoryType,
            scopeType,
            scopeId,
            namespaceValue,
            visibility,
            request.Subject?.Trim() ?? string.Empty,
            request.Predicate?.Trim() ?? string.Empty,
            request.Object?.Trim() ?? string.Empty,
            request.Confidence,
            trustLevel,
            sensitivity);
        return true;
    }

    private static bool IsSupportedScope(string scopeType)
    {
        return scopeType is "global" or "org" or "user" or "project" or "role" or "agent" or "session";
    }

    private static string Normalize(string? value, string defaultValue = "")
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();
    }

    private static ProblemDetails BadRequest(string detail)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Memory proposal is invalid.",
            Detail = detail
        };
    }
}
