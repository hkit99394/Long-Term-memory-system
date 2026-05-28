using MemorySystem.Api.Http;
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
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
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
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
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

    private static bool TryReadLimit(HttpContext context, out int limit, out string? error)
    {
        return ApiRequestHelpers.TryReadLimitQuery(
            context,
            defaultLimit: 50,
            maxLimit: ObsidianExportService.MaxLimit,
            out limit,
            out error);
    }

    private static bool TryReadScope(
        HttpContext context,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        return ApiRequestHelpers.TryReadOptionalTargetScopeQuery(context, out scopeType, out scopeId, out error);
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
