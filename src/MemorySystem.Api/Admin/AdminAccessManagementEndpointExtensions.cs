using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Roles;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Scopes;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Admin;

public static class AdminAccessManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemAdminAccessManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/admin/access/organization-memberships",
            async (
                HttpContext context,
                IAdminAccessManagementStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpsertOrganizationMembershipAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/project-memberships",
            async (
                HttpContext context,
                IAdminAccessManagementStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpsertProjectMembershipAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/project-roles",
            async (
                HttpContext context,
                IProjectRoleDefinitionStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpsertProjectRoleDefinitionAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/role-assignments",
            async (
                HttpContext context,
                IAdminAccessManagementStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpsertRoleAssignmentAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/namespace-grants",
            async (
                HttpContext context,
                IAdminAccessManagementStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await UpsertNamespaceGrantAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/effective-preview",
            async (
                HttpContext context,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await PreviewEffectiveAccessAsync(context, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        endpoints.MapPost(
            "/api/admin/access/permission-drift",
            async (
                HttpContext context,
                IAdminPermissionDriftReportStore store,
                IMemoryAccessAuthorizer accessAuthorizer,
                CancellationToken cancellationToken) =>
                await GeneratePermissionDriftReportAsync(context, store, accessAuthorizer, cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        return endpoints;
    }

    private static async Task<IResult> UpsertOrganizationMembershipAsync(
        HttpContext context,
        IAdminAccessManagementStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminOrganizationMembershipRequest>(
            context,
            "Organization membership request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        var requestedAccessLevel = NormalizeForComparison(request.AccessLevel);
        if (request.PrincipalId == actorPrincipalId && requestedAccessLevel is "admin" or "owner")
        {
            return BadRequest("Organization membership request is invalid.", "Operators cannot grant themselves admin or owner access.");
        }

        var scope = new MemoryScopeResolution("org", request.OrgId.ToString("D"), OrgId: request.OrgId);
        var authorization = await AuthorizeOperatorAsync(accessAuthorizer, actorPrincipalId, scope, namespacePrefix: null, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            var record = await store.UpsertOrganizationMembershipAsync(
                new AdminOrganizationMembershipCommand(
                    actorPrincipalId,
                    request.OrgId,
                    request.PrincipalId,
                    request.AccessLevel),
                cancellationToken);

            return Results.Ok(new AdminOrganizationMembershipResponse(
                record.OrgId,
                record.PrincipalId,
                record.AccessLevel,
                record.CreatedAt));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Organization membership request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpsertProjectMembershipAsync(
        HttpContext context,
        IAdminAccessManagementStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminProjectMembershipRequest>(
            context,
            "Project membership request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        var requestedAccessLevel = NormalizeForComparison(request.AccessLevel);
        if (request.PrincipalId == actorPrincipalId && requestedAccessLevel == "admin")
        {
            return BadRequest("Project membership request is invalid.", "Operators cannot grant themselves admin access.");
        }

        var scope = new MemoryScopeResolution("project", request.ProjectId.ToString("D"), ProjectId: request.ProjectId);
        var authorization = await AuthorizeOperatorAsync(accessAuthorizer, actorPrincipalId, scope, namespacePrefix: null, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            var record = await store.UpsertProjectMembershipAsync(
                new AdminProjectMembershipCommand(
                    actorPrincipalId,
                    request.ProjectId,
                    request.PrincipalId,
                    request.AccessLevel),
                cancellationToken);

            return Results.Ok(new AdminProjectMembershipResponse(
                record.ProjectId,
                record.PrincipalId,
                record.AccessLevel,
                record.CreatedAt));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Project membership request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpsertProjectRoleDefinitionAsync(
        HttpContext context,
        IProjectRoleDefinitionStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminProjectRoleDefinitionRequest>(
            context,
            "Project role definition request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        var scope = new MemoryScopeResolution("project", request.ProjectId.ToString("D"), ProjectId: request.ProjectId);
        var authorization = await AuthorizeOperatorAsync(accessAuthorizer, actorPrincipalId, scope, namespacePrefix: null, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            var record = await store.UpsertAsync(
                new ProjectRoleDefinitionCommand(
                    actorPrincipalId,
                    request.ProjectId,
                    request.RoleId,
                    request.DisplayName,
                    request.Description,
                    request.TemplateRoleId,
                    string.IsNullOrWhiteSpace(request.Status) ? "active" : request.Status),
                cancellationToken);

            return Results.Ok(new AdminProjectRoleDefinitionResponse(
                record.ProjectId,
                record.RoleId,
                record.DisplayName,
                record.Description,
                record.TemplateRoleId,
                record.Status,
                record.CreatedAt,
                record.UpdatedAt));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Project role definition request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpsertRoleAssignmentAsync(
        HttpContext context,
        IAdminAccessManagementStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminRoleAssignmentRequest>(
            context,
            "Role assignment request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        if (request.PrincipalId == actorPrincipalId)
        {
            return BadRequest("Role assignment request is invalid.", "Operators cannot assign roles to themselves.");
        }

        if (!TryCreateManagedScope(request.ScopeType, request.ScopeId.ToString("D"), out var scope, out var error))
        {
            return BadRequest("Role assignment request is invalid.", error!);
        }

        var authorization = await AuthorizeOperatorAsync(accessAuthorizer, actorPrincipalId, scope, namespacePrefix: null, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            var record = await store.UpsertRoleAssignmentAsync(
                new AdminRoleAssignmentCommand(
                    actorPrincipalId,
                    request.PrincipalId,
                    request.RoleId,
                    request.ScopeType,
                    request.ScopeId),
                cancellationToken);

            return Results.Ok(new AdminRoleAssignmentResponse(
                record.AssignmentId,
                record.PrincipalId,
                record.RoleId,
                record.ScopeType,
                record.ScopeId,
                record.CreatedAt));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Role assignment request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> UpsertNamespaceGrantAsync(
        HttpContext context,
        IAdminAccessManagementStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminNamespaceGrantRequest>(
            context,
            "Namespace grant request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        var requestedPermission = NormalizeForComparison(request.Permission);
        if (request.PrincipalId == actorPrincipalId && requestedPermission == MemoryAccessPermissions.Admin)
        {
            return BadRequest("Namespace grant request is invalid.", "Operators cannot grant themselves namespace admin access.");
        }

        if (!TryCreateManagedScope(request.ScopeType, request.ScopeId.ToString("D"), out var scope, out var error))
        {
            return BadRequest("Namespace grant request is invalid.", error!);
        }

        var authorizationNamespace = ShouldAuthorizeProjectRoleNamespaceGrantByScopeOnly(
            scope,
            request.NamespacePrefix)
            ? null
            : request.NamespacePrefix;
        var authorization = await AuthorizeOperatorAsync(
            accessAuthorizer,
            actorPrincipalId,
            scope,
            authorizationNamespace,
            cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            var record = await store.UpsertNamespaceGrantAsync(
                new AdminNamespaceGrantCommand(
                    actorPrincipalId,
                    request.PrincipalId,
                    request.RoleId,
                    scope.ScopeType,
                    Guid.Parse(scope.ScopeId),
                    request.NamespacePrefix,
                    request.Permission),
                cancellationToken);

            return Results.Ok(new AdminNamespaceGrantResponse(
                record.GrantId,
                record.PrincipalId,
                record.RoleId,
                record.NamespacePrefix,
                record.Permission,
                record.CreatedAt));
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Namespace grant request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult> PreviewEffectiveAccessAsync(
        HttpContext context,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminEffectiveAccessPreviewRequest>(
            context,
            "Effective access preview request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        if (!TryCreateManagedScope(request.ScopeType, request.ScopeId, out var scope, out var error))
        {
            return BadRequest("Effective access preview request is invalid.", error!);
        }

        if (!TryNormalizeAllowed(request.Permission, MemoryAccessPermissions.All, "Permission", out var permission, out var permissionError))
        {
            return BadRequest("Effective access preview request is invalid.", permissionError!);
        }

        var authorization = await AuthorizeOperatorAsync(
            accessAuthorizer,
            actorPrincipalId,
            scope,
            request.NamespacePrefix,
            cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        var preview = await accessAuthorizer.PreviewAsync(
            new MemoryAccessRequest(
                request.PrincipalId,
                permission,
                scope,
                NormalizeOptionalNamespace(request.NamespacePrefix)),
            cancellationToken);

        return Results.Ok(new AdminEffectiveAccessPreviewResponse(
            request.PrincipalId,
            permission,
            new AdminAccessScopeResponse(
                scope.ScopeType,
                scope.ScopeId,
                scope.OrgId,
                scope.ProjectId,
                scope.ScopeRoleId),
            NormalizeOptionalNamespace(request.NamespacePrefix),
            preview.Allowed,
            preview.Reason ?? "Allowed by current membership, role, and namespace grant policy.",
            nameof(IMemoryAccessAuthorizer)));
    }

    private static async Task<IResult> GeneratePermissionDriftReportAsync(
        HttpContext context,
        IAdminPermissionDriftReportStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync<AdminPermissionDriftReportRequest>(
            context,
            "Permission-drift report request is invalid.",
            cancellationToken);
        if (!body.Succeeded)
        {
            return body.FailureResult!;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var actorPrincipalId, out var principalFailure))
        {
            return principalFailure;
        }

        var request = body.Value!;
        if (!TryCreateManagedScope(request.ScopeType, request.ScopeId.ToString("D"), out var scope, out var error))
        {
            return BadRequest("Permission-drift report request is invalid.", error!);
        }

        var namespacePrefix = NormalizeOptionalNamespace(request.NamespacePrefix);
        var authorization = await AuthorizeOperatorAsync(
            accessAuthorizer,
            actorPrincipalId,
            scope,
            namespacePrefix,
            cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        var staleAfterDays = request.StaleAfterDays ?? 90;
        var maxPreviewPrincipals = request.MaxPreviewPrincipals ?? 20;

        try
        {
            var report = await store.GenerateAsync(
                new AdminPermissionDriftReportQuery(
                    actorPrincipalId,
                    scope.ScopeType,
                    Guid.Parse(scope.ScopeId),
                    namespacePrefix,
                    staleAfterDays,
                    maxPreviewPrincipals),
                cancellationToken);

            return Results.Ok(report);
        }
        catch (ArgumentException exception)
        {
            return BadRequest("Permission-drift report request is invalid.", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest("Permission-drift report request is invalid.", exception.Message);
        }
    }

    private static async Task<IResult?> AuthorizeOperatorAsync(
        IMemoryAccessAuthorizer accessAuthorizer,
        Guid actorPrincipalId,
        MemoryScopeResolution scope,
        string? namespacePrefix,
        CancellationToken cancellationToken)
    {
        var decision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                actorPrincipalId,
                MemoryAccessPermissions.Admin,
                scope,
                NormalizeOptionalNamespace(namespacePrefix)),
            cancellationToken);

        return decision.Allowed
            ? null
            : Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Admin access-management request is forbidden.",
                detail: decision.Reason);
    }

    private static bool ShouldAuthorizeProjectRoleNamespaceGrantByScopeOnly(
        MemoryScopeResolution scope,
        string namespacePrefix)
    {
        if (scope.ScopeType != MemoryScopeType.Project
            || scope.ProjectId is not Guid projectId)
        {
            return false;
        }

        return MemoryNamespaceParser.TryParse(namespacePrefix, out var memoryNamespace, out _)
            && memoryNamespace.RoleId is not null
            && memoryNamespace.ScopeType == MemoryScopeType.Project
            && memoryNamespace.ScopeId == projectId.ToString("D");
    }

    private static bool TryCreateManagedScope(
        string scopeType,
        string scopeId,
        out MemoryScopeResolution scope,
        out string? error)
    {
        scope = null!;

        if (!MemoryScopePolicy.TryNormalizeTargetScope(scopeType, scopeId, out var normalizedScopeType, out var normalizedScopeId, out error))
        {
            return false;
        }

        scope = normalizedScopeType switch
        {
            "org" => new MemoryScopeResolution("org", normalizedScopeId, OrgId: Guid.Parse(normalizedScopeId)),
            "project" => new MemoryScopeResolution("project", normalizedScopeId, ProjectId: Guid.Parse(normalizedScopeId)),
            _ => null!
        };

        if (scope is not null)
        {
            return true;
        }

        error = "Admin access-management currently supports org and project scopes.";
        return false;
    }

    private static async Task<RequestBodyResult<T>> ReadBodyAsync<T>(
        HttpContext context,
        string title,
        CancellationToken cancellationToken)
    {
        var body = await ApiRequestHelpers.ReadJsonBodyAsync<T>(
            context.Request,
            title,
            cancellationToken);

        return body.Succeeded
            ? RequestBodyResult<T>.Success(body.Value!)
            : RequestBodyResult<T>.Failure(ToResult(body.Problem!));
    }

    private static IResult ToResult(ApiIdempotencyResponse response)
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

    private static string? NormalizeOptionalNamespace(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.TrimEnd('/');
    }

    private static string? NormalizeForComparison(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToLowerInvariant();
    }

    private static bool TryNormalizeAllowed(
        string? value,
        IReadOnlySet<string> allowedValues,
        string fieldName,
        out string normalized,
        out string? error)
    {
        normalized = string.Empty;

        var candidate = NormalizeForComparison(value);
        if (candidate is null)
        {
            error = $"{fieldName} is required.";
            return false;
        }

        if (!allowedValues.Contains(candidate))
        {
            error = $"{fieldName} is not supported.";
            return false;
        }

        normalized = candidate;
        error = null;
        return true;
    }

    private sealed record RequestBodyResult<T>(T? Value, IResult? FailureResult)
    {
        public bool Succeeded => FailureResult is null;

        public static RequestBodyResult<T> Success(T value)
        {
            return new RequestBodyResult<T>(value, null);
        }

        public static RequestBodyResult<T> Failure(IResult failure)
        {
            return new RequestBodyResult<T>(default, failure);
        }
    }
}
