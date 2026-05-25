using System.Diagnostics.CodeAnalysis;
using MemorySystem.Api.Http;
using MemorySystem.Application.Scopes;
using MemorySystem.Application.VaultExports;

namespace MemorySystem.Api.VaultExports;

public static class VaultExportEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemVaultExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/vault/exports/obsidian",
            async (
                HttpContext context,
                IObsidianExportService exportService,
                CancellationToken cancellationToken) =>
                await ExportObsidianAsync(context, exportService, cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/vault/exports/obsidian/archive",
            async (
                HttpContext context,
                IObsidianExportService exportService,
                CancellationToken cancellationToken) =>
                await ExportObsidianArchiveAsync(context, exportService, cancellationToken))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ExportObsidianAsync(
        HttpContext context,
        IObsidianExportService exportService,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadLimit(context, out var limit, out var error)
            || !TryReadScope(context, out var scopeType, out var scopeId, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Obsidian export request is invalid.",
                detail: error);
        }

        var export = await exportService.ExportAsync(
            new ObsidianExportQuery(principalId, scopeType, scopeId, limit),
            cancellationToken);

        return Results.Ok(new ObsidianExportResponse(
            export.GeneratedAt,
            export.Documents.Select(ToResponse).ToArray(),
            export.StaleDocuments.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> ExportObsidianArchiveAsync(
        HttpContext context,
        IObsidianExportService exportService,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadLimit(context, out var limit, out var error)
            || !TryReadScope(context, out var scopeType, out var scopeId, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Obsidian archive export request is invalid.",
                detail: error);
        }

        var export = await exportService.ExportArchiveAsync(
            new ObsidianExportQuery(principalId, scopeType, scopeId, limit),
            cancellationToken);

        return Results.Ok(new ObsidianArchiveExportResponse(
            export.GeneratedAt,
            export.Documents.Select(ToResponse).ToArray()));
    }

    private static bool TryReadPrincipalId(
        HttpContext context,
        out Guid principalId,
        [NotNullWhen(false)] out IResult? failure)
    {
        if (ApiRequestHelpers.TryGetPrincipalId(context, out principalId))
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authenticated principal is invalid.",
            detail: "The API key did not resolve to a valid principal id.");
        return false;
    }

    private static bool TryReadLimit(HttpContext context, out int limit, out string? error)
    {
        limit = 50;
        error = null;
        var limitValue = context.Request.Query["limit"].ToString();

        if (string.IsNullOrWhiteSpace(limitValue))
        {
            return true;
        }

        if (!int.TryParse(limitValue, out limit) || limit < 1 || limit > ObsidianExportService.MaxLimit)
        {
            error = $"Query parameter 'limit' must be between 1 and {ObsidianExportService.MaxLimit}.";
            return false;
        }

        return true;
    }

    private static bool TryReadScope(
        HttpContext context,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        scopeType = context.Request.Query["scopeType"].ToString();
        scopeId = context.Request.Query["scopeId"].ToString();
        error = null;

        if (string.IsNullOrWhiteSpace(scopeType) && string.IsNullOrWhiteSpace(scopeId))
        {
            scopeType = null;
            scopeId = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(scopeType) || string.IsNullOrWhiteSpace(scopeId))
        {
            error = "Query parameters 'scopeType' and 'scopeId' must be provided together.";
            return false;
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out error))
        {
            return false;
        }

        scopeType = normalizedScopeType;
        scopeId = normalizedScopeId;
        return true;
    }

    private static ObsidianExportDocumentResponse ToResponse(ObsidianExportDocument document)
    {
        return new ObsidianExportDocumentResponse(
            document.Path,
            document.Title,
            document.MemoryType,
            document.ScopeType,
            document.ScopeId,
            document.Namespace,
            document.MemoryFactId,
            document.SourceEventId,
            document.SourceLink,
            document.Content);
    }

    private static ObsidianStaleExportDocumentResponse ToResponse(ObsidianStaleExportDocument document)
    {
        return new ObsidianStaleExportDocumentResponse(
            document.Path,
            document.MemoryFactId,
            document.SourceEventId,
            document.SourceLink,
            document.MemoryType,
            document.ScopeType,
            document.ScopeId,
            document.Namespace,
            document.Status,
            document.Reason,
            document.Content);
    }

    private static ObsidianArchiveExportDocumentResponse ToResponse(ObsidianArchiveExportDocument document)
    {
        return new ObsidianArchiveExportDocumentResponse(
            document.Path,
            document.Title,
            document.MemoryFactId,
            document.SourceEventId,
            document.SourceLink,
            document.MemoryType,
            document.ScopeType,
            document.ScopeId,
            document.Namespace,
            document.Status,
            document.Content);
    }
}
