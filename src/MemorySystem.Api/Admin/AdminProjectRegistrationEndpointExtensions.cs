using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Scopes;

namespace MemorySystem.Api.Admin;

public static class AdminProjectRegistrationEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemAdminProjectRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/admin/projects/register",
            async (
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IAdminProjectRegistrationStore store,
                IMemoryAccessAuthorizer accessAuthorizer) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/admin/projects/register",
                    async (idempotencyContext, cancellationToken) =>
                        await RegisterProjectAsync(
                            context,
                            store,
                            accessAuthorizer,
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Admin);

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> RegisterProjectAsync(
        HttpContext context,
        IAdminProjectRegistrationStore store,
        IMemoryAccessAuthorizer accessAuthorizer,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AdminProjectRegistrationRequest>(
            context.Request,
            "Project registration request is invalid.",
            cancellationToken);
        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var request = requestResult.Value!;
        var authorization = await AuthorizeRegistrationAsync(
            accessAuthorizer,
            idempotency.PrincipalId,
            request.OrganizationId,
            request.ProjectId,
            cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            var record = await store.RegisterAsync(
                ToCommand(context, request, idempotency),
                cancellationToken);

            return new ApiIdempotencyResponse(
                StatusCodes.Status200OK,
                ToResponse(record),
                ResourceType: "project_registration",
                ResourceId: record.Project.ProjectId,
                IdempotencyAlreadyCompleted: true);
        }
        catch (ArgumentException exception)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Project registration request is invalid.",
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Project registration request is invalid.",
                exception.Message);
        }
    }

    private static async Task<ApiIdempotencyResponse?> AuthorizeRegistrationAsync(
        IMemoryAccessAuthorizer accessAuthorizer,
        Guid actorPrincipalId,
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var projectScope = new MemoryScopeResolution(
            MemoryScopeType.Project,
            projectId.ToString("D"),
            ProjectId: projectId);
        var projectDecision = await accessAuthorizer.PreviewAsync(
            new MemoryAccessRequest(
                actorPrincipalId,
                MemoryAccessPermissions.Admin,
                projectScope,
                Namespace: null),
            cancellationToken);
        if (projectDecision.Allowed)
        {
            return null;
        }

        var organizationScope = new MemoryScopeResolution(
            MemoryScopeType.Organization,
            organizationId.ToString("D"),
            OrgId: organizationId);
        var organizationDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                actorPrincipalId,
                MemoryAccessPermissions.Admin,
                organizationScope,
                Namespace: null),
            cancellationToken);
        if (organizationDecision.Allowed)
        {
            return null;
        }

        return ApiRequestHelpers.Problem(
            StatusCodes.Status403Forbidden,
            "Project registration request is forbidden.",
            $"Actor must have admin access to the target project or owner/admin access to the target organization. Project check: {projectDecision.Reason} Organization check: {organizationDecision.Reason}");
    }

    private static AdminProjectRegistrationCommand ToCommand(
        HttpContext context,
        AdminProjectRegistrationRequest request,
        ApiIdempotencyExecutionContext idempotency)
    {
        return new AdminProjectRegistrationCommand(
            idempotency.PrincipalId,
            idempotency.RecordId,
            idempotency.RequestHash,
            request.OrganizationId,
            request.OrganizationName,
            request.ProjectId,
            request.ProjectName,
            request.ProjectStatus,
            (request.RoleDefinitions ?? [])
                .Select(role => new AdminProjectRegistrationRoleDefinitionCommand(
                    role.RoleId,
                    role.DisplayName,
                    role.Description,
                    role.TemplateRoleId,
                    role.Status))
                .ToArray(),
            (request.OwnerAssignments ?? [])
                .Select(owner => new AdminProjectRegistrationOwnerAssignmentCommand(
                    owner.PrincipalId,
                    owner.RoleId,
                    owner.ProjectAccessLevel,
                    owner.PrincipalLabel))
                .ToArray(),
            (request.NamespaceGrants ?? [])
                .Select(grant => new AdminProjectRegistrationNamespaceGrantCommand(
                    grant.PrincipalId,
                    grant.RoleId,
                    grant.NamespacePrefix,
                    grant.Permission))
                .ToArray(),
            (request.SourceDocuments ?? [])
                .Select(source => new AdminProjectRegistrationSourceDocumentCommand(
                    source.Path,
                    source.SourceContentSha256,
                    source.SourceOwnerRoleId))
                .ToArray(),
            request.AccessPreviewReportId,
            request.AuditExportId,
            request.RegistrationNote,
            context.Request.Method,
            context.Request.Path.Value,
            context.TraceIdentifier);
    }

    private static AdminProjectRegistrationResponse ToResponse(AdminProjectRegistrationRecord record)
    {
        return new AdminProjectRegistrationResponse(
            record.ContractId,
            record.Status,
            new AdminProjectRegistrationOrganizationResponse(
                record.Organization.OrganizationId,
                record.Organization.OrganizationName,
                record.Organization.CreatedAt,
                record.Organization.UpdatedAt),
            new AdminProjectRegistrationProjectResponse(
                record.Project.ProjectId,
                record.Project.OrganizationId,
                record.Project.ProjectName,
                record.Project.ProjectStatus,
                record.Project.CreatedAt,
                record.Project.UpdatedAt),
            record.RoleDefinitions
                .Select(role => new AdminProjectRegistrationRoleDefinitionResponse(
                    role.ProjectId,
                    role.RoleId,
                    role.DisplayName,
                    role.Description,
                    role.TemplateRoleId,
                    role.Status,
                    role.CreatedAt,
                    role.UpdatedAt))
                .ToArray(),
            record.OwnerAssignments
                .Select(owner => new AdminProjectRegistrationOwnerAssignmentResponse(
                    owner.PrincipalId,
                    owner.RoleId,
                    owner.ProjectAccessLevel,
                    owner.PrincipalLabel,
                    owner.RoleAssignmentId,
                    owner.MembershipCreatedAt,
                    owner.RoleAssignedAt))
                .ToArray(),
            record.NamespaceGrants
                .Select(grant => new AdminProjectRegistrationNamespaceGrantResponse(
                    grant.GrantId,
                    grant.PrincipalId,
                    grant.RoleId,
                    grant.NamespacePrefix,
                    grant.Permission,
                    grant.CreatedAt))
                .ToArray(),
            new AdminProjectRegistrationAuditEvidenceResponse(
                record.AuditEvidence.AuditEventId,
                record.AuditEvidence.OccurredAt,
                record.AuditEvidence.IdempotencyRecordId,
                record.AuditEvidence.RegistrationRequestHash,
                record.AuditEvidence.AccessPreviewReportId,
                record.AuditEvidence.AuditExportId,
                record.AuditEvidence.ActionType,
                record.AuditEvidence.ResourceType,
                record.AuditEvidence.ResourceId),
            record.SourceDocumentCount,
            record.SourceHashCoveragePercent,
            record.PayloadSafe,
            record.RawSourcePayloadsIncluded);
    }
}
