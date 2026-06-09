using System.Globalization;
using MemorySystem.Api.Http;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Scopes;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Admin;

public static class AdminOrganizationProjectManagementEndpointExtensions
{
    private const string ContractId = "OPM-01";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private static readonly IReadOnlySet<string> ProjectStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "planned",
        "active",
        "archived",
        "deleted"
    };

    public static IEndpointRouteBuilder MapMemorySystemAdminOrganizationProjectManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/organizations",
            async (
                HttpContext context,
                IAdminOrganizationProjectManagementStore store,
                CancellationToken cancellationToken) =>
                await ListOrganizationsAsync(context, store, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/organizations/{organizationId:guid}",
            async (
                HttpContext context,
                Guid organizationId,
                IAdminOrganizationProjectManagementStore store,
                CancellationToken cancellationToken) =>
                await GetOrganizationAsync(context, organizationId, store, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/organizations/{organizationId:guid}/management-activity",
            async (
                HttpContext context,
                Guid organizationId,
                IAdminManagementActivityStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetOrganizationManagementActivityAsync(context, organizationId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects",
            async (
                HttpContext context,
                IAdminOrganizationProjectManagementStore store,
                CancellationToken cancellationToken) =>
                await ListProjectsAsync(context, store, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects/{projectId:guid}",
            async (
                HttpContext context,
                Guid projectId,
                IAdminOrganizationProjectManagementStore store,
                CancellationToken cancellationToken) =>
                await GetProjectAsync(context, projectId, store, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects/{projectId:guid}/management-activity",
            async (
                HttpContext context,
                Guid projectId,
                IAdminManagementActivityStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetProjectManagementActivityAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects/{projectId:guid}/role-definitions",
            async (
                HttpContext context,
                Guid projectId,
                IAdminProjectRoleDefinitionManagementStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetProjectRoleDefinitionsAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPut(
            "/api/admin/projects/{projectId:guid}/role-definitions/{roleId}",
            async (
                HttpContext context,
                Guid projectId,
                string roleId,
                IAdminProjectRoleDefinitionManagementStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpdateProjectRoleDefinitionAsync(context, projectId, roleId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects/{projectId:guid}/scope-settings",
            async (
                HttpContext context,
                Guid projectId,
                IAdminProjectLifecycleSettingsStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetProjectScopeSettingsAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPatch(
            "/api/admin/projects/{projectId:guid}/lifecycle",
            async (
                HttpContext context,
                Guid projectId,
                IAdminProjectLifecycleSettingsStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpdateProjectLifecycleAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPut(
            "/api/admin/projects/{projectId:guid}/scope-settings",
            async (
                HttpContext context,
                Guid projectId,
                IAdminProjectLifecycleSettingsStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpdateProjectScopeSettingsAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/organizations/{organizationId:guid}/access-inventory",
            async (
                HttpContext context,
                Guid organizationId,
                IAdminAccessInventoryStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetOrganizationAccessInventoryAsync(context, organizationId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects/{projectId:guid}/access-inventory",
            async (
                HttpContext context,
                Guid projectId,
                IAdminAccessInventoryStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetProjectAccessInventoryAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/revocations",
            async (
                HttpContext context,
                IAdminAccessInventoryStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await RevokeAccessAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapGet(
            "/api/admin/projects/{projectId:guid}/grant-matrix",
            async (
                HttpContext context,
                Guid projectId,
                IAdminGrantMatrixStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GetProjectGrantMatrixAsync(context, projectId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPut(
            "/api/admin/projects/{projectId:guid}/grant-matrix/roles/{roleId}",
            async (
                HttpContext context,
                Guid projectId,
                string roleId,
                IAdminGrantMatrixStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpdateProjectGrantMatrixRoleAsync(context, projectId, roleId, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        return endpoints;
    }

    private static async Task<IResult> ListOrganizationsAsync(
        HttpContext context,
        IAdminOrganizationProjectManagementStore store,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadPaging(context, out var limit, out var offset, out var pagingError))
        {
            return BadRequest("Admin organization list request is invalid.", pagingError!);
        }

        try
        {
            var record = await store.ListOrganizationsAsync(
                new AdminOrganizationListQuery(
                    actorPrincipalId,
                    ReadOptionalQuery(context, "q"),
                    limit,
                    offset),
                cancellationToken);

            var organizations = record.Organizations.Select(ToResponse).ToArray();
            return Results.Ok(new AdminOrganizationListResponse(
                ContractId,
                organizations,
                organizations.Length,
                limit,
                record.NextCursor,
                PayloadSafe: true,
                RawSourcePayloadsIncluded: false));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin organization list request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetOrganizationAsync(
        HttpContext context,
        Guid organizationId,
        IAdminOrganizationProjectManagementStore store,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var record = await store.GetOrganizationAsync(
                new AdminOrganizationDetailQuery(actorPrincipalId, organizationId),
                cancellationToken);
            if (record is null)
            {
                return NotFound("Admin organization detail was not found.", "Organization was not found or is not visible to the caller.");
            }

            return Results.Ok(new AdminOrganizationDetailResponse(
                ContractId,
                ToResponse(record.Organization),
                PayloadSafe: true,
                RawSourcePayloadsIncluded: false));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin organization detail request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetOrganizationManagementActivityAsync(
        HttpContext context,
        Guid organizationId,
        IAdminManagementActivityStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadPaging(context, out var limit, out var offset, out var pagingError))
        {
            return BadRequest("Admin organization management activity request is invalid.", pagingError!);
        }

        try
        {
            var record = await store.GetOrganizationActivityAsync(
                new AdminManagementActivityQuery(actorPrincipalId, organizationId, limit, offset),
                cancellationToken);
            if (record is null)
            {
                return NotFound("Admin organization management activity was not found.", "Organization was not found.");
            }

            var authorization = await AuthorizeOrganizationManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                organizationId,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            return Results.Ok(ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin organization management activity request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> ListProjectsAsync(
        HttpContext context,
        IAdminOrganizationProjectManagementStore store,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadPaging(context, out var limit, out var offset, out var pagingError))
        {
            return BadRequest("Admin project list request is invalid.", pagingError!);
        }

        if (!TryReadOptionalGuid(context, "orgId", out var organizationId, out var orgError))
        {
            return BadRequest("Admin project list request is invalid.", orgError!);
        }

        if (!TryReadOptionalProjectStatus(context, out var projectStatus, out var statusError))
        {
            return BadRequest("Admin project list request is invalid.", statusError!);
        }

        try
        {
            var record = await store.ListProjectsAsync(
                new AdminProjectListQuery(
                    actorPrincipalId,
                    organizationId,
                    projectStatus,
                    ReadOptionalQuery(context, "q"),
                    limit,
                    offset),
                cancellationToken);

            var projects = record.Projects.Select(ToResponse).ToArray();
            return Results.Ok(new AdminProjectListResponse(
                ContractId,
                projects,
                projects.Length,
                limit,
                record.NextCursor,
                PayloadSafe: true,
                RawSourcePayloadsIncluded: false));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project list request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetProjectAsync(
        HttpContext context,
        Guid projectId,
        IAdminOrganizationProjectManagementStore store,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var record = await store.GetProjectAsync(
                new AdminProjectDetailQuery(actorPrincipalId, projectId),
                cancellationToken);
            if (record is null)
            {
                return NotFound("Admin project detail was not found.", "Project was not found or is not visible to the caller.");
            }

            return Results.Ok(new AdminProjectDetailResponse(
                ContractId,
                ToResponse(record.Project),
                record.LatestRegistrationEvidence is null ? null : ToResponse(record.LatestRegistrationEvidence),
                PayloadSafe: true,
                RawSourcePayloadsIncluded: false));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project detail request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetProjectManagementActivityAsync(
        HttpContext context,
        Guid projectId,
        IAdminManagementActivityStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadPaging(context, out var limit, out var offset, out var pagingError))
        {
            return BadRequest("Admin project management activity request is invalid.", pagingError!);
        }

        try
        {
            var record = await store.GetProjectActivityAsync(
                new AdminManagementActivityQuery(actorPrincipalId, projectId, limit, offset),
                cancellationToken);
            if (record is null)
            {
                return NotFound("Admin project management activity was not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                ToProjectManagementContext(record.Scope),
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            return Results.Ok(ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project management activity request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetProjectRoleDefinitionsAsync(
        HttpContext context,
        Guid projectId,
        IAdminProjectRoleDefinitionManagementStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project role definitions were not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var record = await store.GetProjectRoleDefinitionsAsync(projectId, cancellationToken);
            return record is null
                ? NotFound("Admin project role definitions were not found.", "Project was not found.")
                : Results.Ok(ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project role definitions request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpdateProjectRoleDefinitionAsync(
        HttpContext context,
        Guid projectId,
        string routeRoleId,
        IAdminProjectRoleDefinitionManagementStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminProjectRoleDefinitionManagementUpdateRequest>(
            context.Request,
            "Admin project role definition request is invalid.",
            cancellationToken);
        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = requestResult.Value!;
        if (!RoleIdsMatch(routeRoleId, request.RoleId))
        {
            return BadRequest("Admin project role definition request is invalid.", "Route role id must match request role id.");
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project role definition was not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var record = await store.UpsertProjectRoleDefinitionAsync(
                new AdminProjectRoleDefinitionUpdateCommand(
                    actorPrincipalId,
                    projectId,
                    routeRoleId,
                    request.RoleId,
                    request.DisplayName,
                    request.Description,
                    request.TemplateRoleId,
                    request.Status,
                    request.Reason,
                    request.AuditEvidenceId,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.TraceIdentifier),
                cancellationToken);

            return Results.Ok(ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project role definition request is invalid.", exception.Message);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound("Admin project role definition was not found.", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest("Admin project role definition request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetProjectScopeSettingsAsync(
        HttpContext context,
        Guid projectId,
        IAdminProjectLifecycleSettingsStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project scope settings were not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var settings = await store.GetScopeSettingsAsync(projectId, cancellationToken);
            return Results.Ok(new AdminProjectScopeSettingsDetailResponse(
                "OPM-03",
                ToResponse(project),
                ToResponse(settings),
                PayloadSafe: true,
                RawSourcePayloadsIncluded: false));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project scope settings request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpdateProjectLifecycleAsync(
        HttpContext context,
        Guid projectId,
        IAdminProjectLifecycleSettingsStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminProjectLifecycleUpdateRequest>(
            context.Request,
            "Admin project lifecycle request is invalid.",
            cancellationToken);
        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project lifecycle was not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var request = requestResult.Value!;
            var record = await store.UpdateLifecycleAsync(
                new AdminProjectLifecycleUpdateCommand(
                    actorPrincipalId,
                    projectId,
                    request.ProjectStatus,
                    request.Reason,
                    request.AuditEvidenceId,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.TraceIdentifier),
                cancellationToken);

            return Results.Ok(new AdminProjectLifecycleUpdateResponse(
                record.ContractId,
                record.Status,
                ToResponse(record.Project),
                record.PreviousProjectStatus,
                ToResponse(record.AuditEvidence),
                record.PayloadSafe,
                record.RawSourcePayloadsIncluded));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project lifecycle request is invalid.", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest("Admin project lifecycle request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpdateProjectScopeSettingsAsync(
        HttpContext context,
        Guid projectId,
        IAdminProjectLifecycleSettingsStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminProjectScopeSettingsUpdateRequest>(
            context.Request,
            "Admin project scope settings request is invalid.",
            cancellationToken);
        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project scope settings were not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var request = requestResult.Value!;
            var record = await store.UpdateScopeSettingsAsync(
                new AdminProjectScopeSettingsUpdateCommand(
                    actorPrincipalId,
                    projectId,
                    request.DefaultNamespacePrefix,
                    request.SourceHashRequired,
                    request.MemoryRetentionClass,
                    request.ReviewCadenceDays,
                    request.Reason,
                    request.AuditEvidenceId,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.TraceIdentifier),
                cancellationToken);

            return Results.Ok(new AdminProjectScopeSettingsUpdateResponse(
                record.ContractId,
                record.Status,
                ToResponse(record.Project),
                ToResponse(record.ScopeSettings),
                ToResponse(record.AuditEvidence),
                record.PayloadSafe,
                record.RawSourcePayloadsIncluded));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project scope settings request is invalid.", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest("Admin project scope settings request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetOrganizationAccessInventoryAsync(
        HttpContext context,
        Guid organizationId,
        IAdminAccessInventoryStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var inventory = await store.GetOrganizationInventoryAsync(organizationId, cancellationToken);
            if (inventory is null)
            {
                return NotFound("Admin organization access inventory was not found.", "Organization was not found.");
            }

            var authorization = await AuthorizeOrganizationManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                organizationId,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            return Results.Ok(ToResponse(inventory));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin organization access inventory request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetProjectAccessInventoryAsync(
        HttpContext context,
        Guid projectId,
        IAdminAccessInventoryStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var inventory = await store.GetProjectInventoryAsync(projectId, cancellationToken);
            if (inventory is null)
            {
                return NotFound("Admin project access inventory was not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                ToProjectManagementContext(inventory.Scope),
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            return Results.Ok(ToResponse(inventory));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project access inventory request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> RevokeAccessAsync(
        HttpContext context,
        IAdminAccessInventoryStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminAccessRevocationRequest>(
            context.Request,
            "Admin access revocation request is invalid.",
            cancellationToken);
        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = requestResult.Value!;
        try
        {
            var authorization = await AuthorizeRevocationScopeAsync(
                store,
                accessAuthorizer,
                actorPrincipalId,
                request.ScopeType,
                request.ScopeId,
                cancellationToken);
            if (authorization.Result is not null)
            {
                return authorization.Result;
            }

            var record = await store.RevokeAccessAsync(
                new AdminAccessRevocationCommand(
                    actorPrincipalId,
                    request.ScopeType,
                    request.ScopeId,
                    request.AccessRecordType,
                    request.AccessRecordId,
                    request.PrincipalId,
                    request.Reason,
                    request.AuditEvidenceId,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.TraceIdentifier),
                cancellationToken);

            return Results.Ok(ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin access revocation request is invalid.", exception.Message);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound("Admin access revocation target was not found.", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest("Admin access revocation request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> GetProjectGrantMatrixAsync(
        HttpContext context,
        Guid projectId,
        IAdminGrantMatrixStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project grant matrix was not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var matrix = await store.GetProjectGrantMatrixAsync(projectId, cancellationToken);
            return matrix is null
                ? NotFound("Admin project grant matrix was not found.", "Project was not found.")
                : Results.Ok(ToResponse(matrix));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project grant matrix request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpdateProjectGrantMatrixRoleAsync(
        HttpContext context,
        Guid projectId,
        string routeRoleId,
        IAdminGrantMatrixStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminGrantMatrixUpdateRequest>(
            context.Request,
            "Admin project grant matrix request is invalid.",
            cancellationToken);
        if (!requestResult.Succeeded)
        {
            return ToResult(requestResult.Problem!);
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = requestResult.Value!;
        if (!RoleIdsMatch(routeRoleId, request.RoleId))
        {
            return BadRequest("Admin project grant matrix request is invalid.", "Route role id must match request role id.");
        }

        try
        {
            var project = await store.GetProjectContextAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound("Admin project grant matrix was not found.", "Project was not found.");
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                project,
                cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            var record = await store.ReplaceRoleGrantsAsync(
                new AdminGrantMatrixReplaceCommand(
                    actorPrincipalId,
                    projectId,
                    request.RoleId,
                    request.PresetId,
                    (request.Grants ?? []).Select(grant => new AdminGrantMatrixGrantCommand(
                        grant.NamespacePrefix,
                        grant.Permission)).ToArray(),
                    request.Reason,
                    request.AuditEvidenceId,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.TraceIdentifier),
                cancellationToken);

            return Results.Ok(ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Admin project grant matrix request is invalid.", exception.Message);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound("Admin project grant matrix was not found.", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest("Admin project grant matrix request is invalid.", exception.Message);
        }
    }

    private static AdminOrganizationSummaryResponse ToResponse(AdminOrganizationSummaryRecord record)
    {
        return new AdminOrganizationSummaryResponse(
            record.OrganizationId,
            record.OrganizationName,
            record.ActorAccessLevel,
            record.CreatedAt,
            record.UpdatedAt,
            new AdminProjectStatusCountsResponse(
                record.ProjectStatusCounts.Planned,
                record.ProjectStatusCounts.Active,
                record.ProjectStatusCounts.Archived,
                record.ProjectStatusCounts.Deleted),
            record.ProjectCount,
            record.OrganizationMembershipCount,
            record.ProjectMembershipCount,
            record.RoleAssignmentCount,
            record.NamespaceGrantCount);
    }

    private static AdminProjectSummaryResponse ToResponse(AdminProjectSummaryRecord record)
    {
        return new AdminProjectSummaryResponse(
            record.ProjectId,
            record.OrganizationId,
            record.OrganizationName,
            record.ProjectName,
            record.ProjectStatus,
            record.ActorAccessLevel,
            record.CreatedAt,
            record.UpdatedAt,
            record.ProjectMembershipCount,
            record.RoleDefinitionCount,
            record.ActiveRoleDefinitionCount,
            record.RoleAssignmentCount,
            record.NamespaceGrantCount);
    }

    private static AdminProjectRegistrationEvidenceResponse ToResponse(AdminProjectRegistrationEvidenceRecord record)
    {
        return new AdminProjectRegistrationEvidenceResponse(
            record.AuditEventId,
            record.OccurredAt,
            record.IdempotencyRecordId,
            record.RegistrationRequestHash,
            record.AccessPreviewReportId,
            record.AuditExportId,
            record.SourceDocumentCount,
            record.SourceHashCoveragePercent,
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminProjectManagementContextResponse ToResponse(AdminProjectManagementContextRecord record)
    {
        return new AdminProjectManagementContextResponse(
            record.ProjectId,
            record.OrganizationId,
            record.ProjectName,
            record.ProjectStatus);
    }

    private static AdminProjectScopeSettingsResponse ToResponse(AdminProjectScopeSettingsRecord record)
    {
        return new AdminProjectScopeSettingsResponse(
            record.ProjectId,
            record.DefaultNamespacePrefix,
            record.SourceHashRequired,
            record.MemoryRetentionClass,
            record.ReviewCadenceDays,
            record.UpdatedByPrincipalId,
            record.CreatedAt,
            record.UpdatedAt,
            record.IsDefault);
    }

    private static AdminProjectManagementAuditEvidenceResponse ToResponse(
        AdminProjectManagementAuditEvidenceRecord record)
    {
        return new AdminProjectManagementAuditEvidenceResponse(
            record.AuditEventId,
            record.OccurredAt,
            record.ActionType,
            record.ResourceType,
            record.ResourceId,
            record.AuditEvidenceId);
    }

    private static AdminManagementActivityResponse ToResponse(AdminManagementActivityRecord record)
    {
        return new AdminManagementActivityResponse(
            record.ContractId,
            ToResponse(record.Scope),
            record.Entries.Select(ToResponse).ToArray(),
            record.ReturnedCount,
            record.Limit,
            record.NextCursor,
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminManagementActivityScopeResponse ToResponse(AdminManagementActivityScopeRecord record)
    {
        return new AdminManagementActivityScopeResponse(
            record.ScopeType,
            record.ScopeId,
            record.OrganizationId,
            record.OrganizationName,
            record.ProjectId,
            record.ProjectName,
            record.ProjectStatus);
    }

    private static AdminManagementActivityEntryResponse ToResponse(AdminManagementActivityEntryRecord record)
    {
        return new AdminManagementActivityEntryResponse(
            record.AuditEventId,
            record.OccurredAt,
            record.ActorPrincipalId,
            record.TargetPrincipalId,
            record.ActionType,
            record.Outcome,
            record.ScopeType,
            record.ScopeId,
            record.RoleId,
            record.NamespacePrefix,
            record.Permission,
            record.ResourceType,
            record.ResourceId,
            record.RequestMethod,
            record.RequestPath,
            record.CorrelationId,
            record.Operation,
            record.SourceContractId,
            record.AuditEvidenceId,
            record.Summary,
            record.Metadata.Select(ToResponse).ToArray());
    }

    private static AdminManagementActivityMetadataResponse ToResponse(AdminManagementActivityMetadataRecord record)
    {
        return new AdminManagementActivityMetadataResponse(record.Key, record.Value);
    }

    private static AdminProjectRoleDefinitionListResponse ToResponse(AdminProjectRoleDefinitionListRecord record)
    {
        return new AdminProjectRoleDefinitionListResponse(
            record.ContractId,
            ToResponse(record.Project),
            record.Roles.Select(ToResponse).ToArray(),
            record.ActiveCount,
            record.DisabledCount,
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminProjectRoleDefinitionUpdateResponse ToResponse(AdminProjectRoleDefinitionUpdateRecord record)
    {
        return new AdminProjectRoleDefinitionUpdateResponse(
            record.ContractId,
            record.Status,
            ToResponse(record.Project),
            ToResponse(record.Role),
            ToResponse(record.AuditEvidence),
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminProjectRoleDefinitionManagementResponse ToResponse(AdminProjectRoleDefinitionRecord record)
    {
        return new AdminProjectRoleDefinitionManagementResponse(
            record.ProjectId,
            record.RoleId,
            record.DisplayName,
            record.Description,
            record.TemplateRoleId,
            record.Status,
            record.AssignmentCount,
            record.RoleGrantCount,
            record.CreatedAt,
            record.UpdatedAt);
    }

    private static AdminGrantMatrixResponse ToResponse(AdminGrantMatrixRecord record)
    {
        return new AdminGrantMatrixResponse(
            record.ContractId,
            ToResponse(record.Project),
            record.Presets.Select(ToResponse).ToArray(),
            record.Roles.Select(ToResponse).ToArray(),
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminGrantMatrixUpdateResponse ToResponse(AdminGrantMatrixUpdateRecord record)
    {
        return new AdminGrantMatrixUpdateResponse(
            record.ContractId,
            record.Status,
            ToResponse(record.Project),
            ToResponse(record.Role),
            ToResponse(record.AuditEvidence),
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminGrantMatrixPresetResponse ToResponse(AdminGrantMatrixPresetRecord record)
    {
        return new AdminGrantMatrixPresetResponse(
            record.PresetId,
            record.DisplayName,
            record.Description,
            record.Grants.Select(ToResponse).ToArray());
    }

    private static AdminGrantMatrixPresetGrantResponse ToResponse(AdminGrantMatrixPresetGrantRecord record)
    {
        return new AdminGrantMatrixPresetGrantResponse(
            record.NamespaceTemplate,
            record.Permission);
    }

    private static AdminGrantMatrixRoleResponse ToResponse(AdminGrantMatrixRoleRecord record)
    {
        return new AdminGrantMatrixRoleResponse(
            record.RoleId,
            record.DisplayName,
            record.Description,
            record.TemplateRoleId,
            record.Status,
            record.RecommendedPresetId,
            record.PresetAlignment,
            record.Grants.Select(ToResponse).ToArray(),
            record.EffectiveAccessPreviews.Select(ToResponse).ToArray());
    }

    private static AdminGrantMatrixGrantResponse ToResponse(AdminGrantMatrixGrantRecord record)
    {
        return new AdminGrantMatrixGrantResponse(
            record.GrantId,
            record.NamespacePrefix,
            record.Permission,
            record.CreatedAt,
            record.FromPreset);
    }

    private static AdminGrantMatrixEffectivePreviewResponse ToResponse(AdminGrantMatrixEffectivePreviewRecord record)
    {
        return new AdminGrantMatrixEffectivePreviewResponse(
            record.PrincipalId,
            record.RoleId,
            record.Permission,
            record.NamespacePrefix,
            record.Allowed,
            record.Reason,
            record.EvaluatedBy);
    }

    private static AdminAccessInventoryResponse ToResponse(AdminAccessInventoryRecord record)
    {
        return new AdminAccessInventoryResponse(
            record.ContractId,
            ToResponse(record.Scope),
            new AdminAccessInventoryCountsResponse(
                record.Counts.OrganizationMemberships,
                record.Counts.ProjectMemberships,
                record.Counts.RoleAssignments,
                record.Counts.NamespaceGrants,
                record.Counts.StaleAccessPrompts),
            record.OrganizationMemberships.Select(ToResponse).ToArray(),
            record.ProjectMemberships.Select(ToResponse).ToArray(),
            record.RoleAssignments.Select(ToResponse).ToArray(),
            record.NamespaceGrants.Select(ToResponse).ToArray(),
            record.StaleAccessPrompts.Select(ToResponse).ToArray(),
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminAccessRevocationResponse ToResponse(AdminAccessRevocationRecord record)
    {
        return new AdminAccessRevocationResponse(
            record.ContractId,
            record.Status,
            ToResponse(record.Scope),
            ToResponse(record.RevokedAccess),
            ToResponse(record.AuditEvidence),
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }

    private static AdminAccessInventoryScopeResponse ToResponse(AdminAccessInventoryScopeRecord record)
    {
        return new AdminAccessInventoryScopeResponse(
            record.ScopeType,
            record.ScopeId,
            record.OrganizationId,
            record.OrganizationName,
            record.ProjectId,
            record.ProjectName,
            record.ProjectStatus);
    }

    private static AdminOrganizationMembershipInventoryResponse ToResponse(
        AdminOrganizationMembershipInventoryRecord record)
    {
        return new AdminOrganizationMembershipInventoryResponse(
            record.AccessRecordType,
            record.AccessRecordId,
            record.OrganizationId,
            record.OrganizationName,
            record.PrincipalId,
            record.PrincipalDisplayName,
            record.PrincipalStatus,
            record.AccessLevel,
            record.CreatedAt,
            record.ReviewPrompt);
    }

    private static AdminProjectMembershipInventoryResponse ToResponse(AdminProjectMembershipInventoryRecord record)
    {
        return new AdminProjectMembershipInventoryResponse(
            record.AccessRecordType,
            record.AccessRecordId,
            record.ProjectId,
            record.ProjectName,
            record.ProjectStatus,
            record.PrincipalId,
            record.PrincipalDisplayName,
            record.PrincipalStatus,
            record.AccessLevel,
            record.CreatedAt,
            record.ReviewPrompt);
    }

    private static AdminRoleAssignmentInventoryResponse ToResponse(AdminRoleAssignmentInventoryRecord record)
    {
        return new AdminRoleAssignmentInventoryResponse(
            record.AccessRecordType,
            record.AccessRecordId,
            record.ScopeType,
            record.ScopeId,
            record.ScopeName,
            record.ProjectStatus,
            record.PrincipalId,
            record.PrincipalDisplayName,
            record.PrincipalStatus,
            record.RoleId,
            record.CreatedAt,
            record.ReviewPrompt);
    }

    private static AdminNamespaceGrantInventoryResponse ToResponse(AdminNamespaceGrantInventoryRecord record)
    {
        return new AdminNamespaceGrantInventoryResponse(
            record.AccessRecordType,
            record.AccessRecordId,
            record.ScopeType,
            record.ScopeId,
            record.ScopeName,
            record.ProjectStatus,
            record.PrincipalId,
            record.PrincipalDisplayName,
            record.PrincipalStatus,
            record.RoleId,
            record.NamespacePrefix,
            record.Permission,
            record.CreatedAt,
            record.ReviewPrompt);
    }

    private static AdminStaleAccessPromptResponse ToResponse(AdminStaleAccessPromptRecord record)
    {
        return new AdminStaleAccessPromptResponse(
            record.AccessRecordType,
            record.AccessRecordId,
            record.Severity,
            record.Prompt);
    }

    private static AdminRevokedAccessResponse ToResponse(AdminRevokedAccessRecord record)
    {
        return new AdminRevokedAccessResponse(
            record.AccessRecordType,
            record.AccessRecordId,
            record.PrincipalId,
            record.PrincipalDisplayName,
            record.RoleId,
            record.NamespacePrefix,
            record.Permission,
            record.Reason,
            record.AuditEvidenceId);
    }

    private static AdminProjectManagementContextRecord ToProjectManagementContext(
        AdminAccessInventoryScopeRecord scope)
    {
        if (scope.ProjectId is not Guid projectId
            || string.IsNullOrWhiteSpace(scope.ProjectName)
            || string.IsNullOrWhiteSpace(scope.ProjectStatus))
        {
            throw new ArgumentException("Project access inventory scope is invalid.");
        }

        return new AdminProjectManagementContextRecord(
            projectId,
            scope.OrganizationId,
            scope.ProjectName,
            scope.ProjectStatus);
    }

    private static AdminProjectManagementContextRecord ToProjectManagementContext(
        AdminManagementActivityScopeRecord scope)
    {
        if (scope.ProjectId is not Guid projectId
            || string.IsNullOrWhiteSpace(scope.ProjectName)
            || string.IsNullOrWhiteSpace(scope.ProjectStatus))
        {
            throw new ArgumentException("Project management activity scope is invalid.");
        }

        return new AdminProjectManagementContextRecord(
            projectId,
            scope.OrganizationId,
            scope.ProjectName,
            scope.ProjectStatus);
    }

    private static async Task<RevocationAuthorizationResult> AuthorizeRevocationScopeAsync(
        IAdminAccessInventoryStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        Guid actorPrincipalId,
        string scopeType,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeType))
        {
            return new RevocationAuthorizationResult(
                BadRequest("Admin access revocation request is invalid.", "Scope type is required."));
        }

        var normalizedScopeType = scopeType.Trim().ToLowerInvariant();
        if (normalizedScopeType == MemoryScopeType.Organization)
        {
            var inventory = await store.GetOrganizationInventoryAsync(scopeId, cancellationToken);
            if (inventory is null)
            {
                return new RevocationAuthorizationResult(
                    NotFound("Admin access revocation scope was not found.", "Organization was not found."));
            }

            var authorization = await AuthorizeOrganizationManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                scopeId,
                cancellationToken);

            return new RevocationAuthorizationResult(authorization);
        }

        if (normalizedScopeType == MemoryScopeType.Project)
        {
            var inventory = await store.GetProjectInventoryAsync(scopeId, cancellationToken);
            if (inventory is null)
            {
                return new RevocationAuthorizationResult(
                    NotFound("Admin access revocation scope was not found.", "Project was not found."));
            }

            var authorization = await AuthorizeProjectManagementAsync(
                accessAuthorizer,
                actorPrincipalId,
                ToProjectManagementContext(inventory.Scope),
                cancellationToken);

            return new RevocationAuthorizationResult(authorization);
        }

        return new RevocationAuthorizationResult(
            BadRequest("Admin access revocation request is invalid.", "Scope type must be org or project."));
    }

    private static async Task<IResult?> AuthorizeOrganizationManagementAsync(
        IMemoryAccessAuthorizer accessAuthorizer,
        Guid actorPrincipalId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organizationDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                actorPrincipalId,
                MemoryAccessPermissions.Admin,
                new MemoryScopeResolution(
                    MemoryScopeType.Organization,
                    organizationId.ToString("D"),
                    OrgId: organizationId),
                Namespace: null),
            cancellationToken);

        return organizationDecision.Allowed
            ? null
            : Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Admin organization management request is forbidden.",
                detail: organizationDecision.Reason);
    }

    private static async Task<IResult?> AuthorizeProjectManagementAsync(
        IMemoryAccessAuthorizer accessAuthorizer,
        Guid actorPrincipalId,
        AdminProjectManagementContextRecord project,
        CancellationToken cancellationToken)
    {
        MemoryAccessDecision? projectDecision = null;
        if (project.ProjectStatus == "active")
        {
            projectDecision = await accessAuthorizer.PreviewAsync(
                new MemoryAccessRequest(
                    actorPrincipalId,
                    MemoryAccessPermissions.Admin,
                    new MemoryScopeResolution(
                        MemoryScopeType.Project,
                        project.ProjectId.ToString("D"),
                        OrgId: project.OrganizationId,
                        ProjectId: project.ProjectId),
                    Namespace: null),
                cancellationToken);

            if (projectDecision.Allowed)
            {
                return null;
            }
        }

        var organizationDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                actorPrincipalId,
                MemoryAccessPermissions.Admin,
                new MemoryScopeResolution(
                    MemoryScopeType.Organization,
                    project.OrganizationId.ToString("D"),
                    OrgId: project.OrganizationId),
                Namespace: null),
            cancellationToken);
        if (organizationDecision.Allowed)
        {
            return null;
        }

        var projectReason = projectDecision is null
            ? $"Project {project.ProjectId:D} is not active; project-scope admin management requires organization admin or owner access."
            : projectDecision.Reason;

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Admin project management request is forbidden.",
            detail: $"Actor must have admin access to the active project or owner/admin access to the parent organization. Project check: {projectReason} Organization check: {organizationDecision.Reason}");
    }

    private static bool TryReadPaging(
        HttpContext context,
        out int limit,
        out int offset,
        out string? error)
    {
        offset = 0;
        if (!ApiRequestHelpers.TryReadLimitQuery(context, DefaultLimit, MaxLimit, out limit, out error))
        {
            return false;
        }

        var cursor = ReadOptionalQuery(context, "cursor");
        if (cursor is null)
        {
            return true;
        }

        if (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 0)
        {
            error = "Query parameter 'cursor' must be a non-negative integer cursor returned by a previous page.";
            return false;
        }

        return true;
    }

    private static bool TryReadOptionalGuid(
        HttpContext context,
        string key,
        out Guid? value,
        out string? error)
    {
        value = null;
        error = null;
        var text = ReadOptionalQuery(context, key);
        if (text is null)
        {
            return true;
        }

        if (Guid.TryParse(text, out var id) && id != Guid.Empty)
        {
            value = id;
            return true;
        }

        error = $"Query parameter '{key}' must be a non-empty GUID.";
        return false;
    }

    private static bool TryReadOptionalProjectStatus(
        HttpContext context,
        out string? status,
        out string? error)
    {
        status = ReadOptionalQuery(context, "status")?.ToLowerInvariant();
        error = null;
        if (status is null || ProjectStatuses.Contains(status))
        {
            return true;
        }

        error = "Query parameter 'status' must be one of planned, active, archived, or deleted.";
        return false;
    }

    private static string? ReadOptionalQuery(HttpContext context, string key)
    {
        var value = context.Request.Query[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool RoleIdsMatch(string routeRoleId, string requestRoleId)
    {
        var route = routeRoleId?.Trim().ToLowerInvariant();
        var request = requestRoleId?.Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(route)
            && !string.IsNullOrWhiteSpace(request)
            && string.Equals(route, request, StringComparison.Ordinal);
    }

    private static IResult ToResult(MemorySystem.Api.Idempotency.ApiIdempotencyResponse response)
    {
        return Results.Json(
            response.Body,
            statusCode: response.StatusCode,
            contentType: response.ContentType ?? "application/problem+json; charset=utf-8");
    }

    private static IResult BadRequest(string title, string detail)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: title,
            detail: detail);
    }

    private static IResult NotFound(string title, string detail)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: title,
            detail: detail);
    }

    private sealed record RevocationAuthorizationResult(IResult? Result);
}
