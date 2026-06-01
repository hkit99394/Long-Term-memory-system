using System.Security.Cryptography;
using System.Text;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminPermissionDriftReportStore(
    NpgsqlDataSource dataSource,
    IMemoryAccessAuthorizer accessAuthorizer) : IAdminPermissionDriftReportStore
{
    private const int MaxSupportedPreviewPrincipals = 100;

    public async Task<AdminPermissionDriftReport> GenerateAsync(
        AdminPermissionDriftReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var scope = await ResolveScopeAsync(connection, query.ScopeType, query.ScopeId, cancellationToken);
        var namespacePrefix = NormalizeNamespacePrefix(query.NamespacePrefix) ?? DefaultNamespacePrefix(scope);
        var generatedAt = DateTimeOffset.UtcNow;
        var staleBefore = generatedAt.AddDays(-query.StaleAfterDays);

        var organizationMemberships = await ReadOrganizationMembershipsAsync(connection, scope, cancellationToken);
        var projectMemberships = await ReadProjectMembershipsAsync(connection, scope, cancellationToken);
        var roleAssignments = await ReadRoleAssignmentsAsync(connection, scope, cancellationToken);
        var namespaceGrants = await ReadNamespaceGrantsAsync(connection, scope, namespacePrefix, query.NamespacePrefix is not null, cancellationToken);
        var serviceAccounts = await ReadServiceAccountsAsync(connection, scope, cancellationToken);
        var serviceCredentials = await ReadServiceCredentialsAsync(
            connection,
            serviceAccounts.Select(account => account.ServicePrincipalId).ToArray(),
            cancellationToken);

        var candidatePrincipalIds = CollectCandidatePrincipalIds(
            organizationMemberships,
            projectMemberships,
            roleAssignments,
            namespaceGrants,
            serviceAccounts,
            serviceCredentials);
        var identityBindings = await ReadIdentityBindingsAsync(connection, candidatePrincipalIds, cancellationToken);
        candidatePrincipalIds.UnionWith(identityBindings.Select(binding => binding.PrincipalId));
        var principals = await ReadPrincipalsAsync(connection, candidatePrincipalIds, cancellationToken);
        var effectiveAccessPreviews = await BuildEffectiveAccessPreviewsAsync(
            scope,
            namespacePrefix,
            principals.Select(principal => principal.PrincipalId).Order().Take(query.MaxPreviewPrincipals),
            cancellationToken);
        var findings = BuildFindings(
            scope,
            namespacePrefix,
            staleBefore,
            generatedAt,
            principals,
            identityBindings,
            serviceAccounts,
            serviceCredentials,
            organizationMemberships,
            projectMemberships,
            roleAssignments,
            namespaceGrants,
            effectiveAccessPreviews);

        return new AdminPermissionDriftReport(
            Guid.NewGuid(),
            generatedAt,
            scope,
            namespacePrefix,
            query.StaleAfterDays,
            principals,
            identityBindings,
            serviceAccounts,
            serviceCredentials,
            organizationMemberships,
            projectMemberships,
            roleAssignments,
            namespaceGrants,
            effectiveAccessPreviews,
            findings);
    }

    private static void ValidateQuery(AdminPermissionDriftReportQuery query)
    {
        if (query.ActorPrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Actor principal id is required.");
        }

        if (query.ScopeId == Guid.Empty)
        {
            throw new ArgumentException("Scope id is required.");
        }

        if (query.ScopeType is not "org" and not "project")
        {
            throw new ArgumentException("Permission-drift reports currently support org and project scopes.");
        }

        if (query.StaleAfterDays <= 0)
        {
            throw new ArgumentException("Stale-after days must be greater than zero.");
        }

        if (query.MaxPreviewPrincipals is < 1 or > MaxSupportedPreviewPrincipals)
        {
            throw new ArgumentException($"Max preview principals must be between 1 and {MaxSupportedPreviewPrincipals}.");
        }
    }

    private static async Task<AdminPermissionDriftScope> ResolveScopeAsync(
        NpgsqlConnection connection,
        string scopeType,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        if (scopeType == "org")
        {
            await using var orgCommand = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM organizations
                    WHERE id = @org_id
                );
                """,
                connection);
            orgCommand.Parameters.AddWithValue("org_id", scopeId);

            if (await orgCommand.ExecuteScalarAsync(cancellationToken) is not true)
            {
                throw new InvalidOperationException($"Organization {scopeId:D} was not found.");
            }

            return new AdminPermissionDriftScope("org", scopeId, scopeId, null);
        }

        await using var projectCommand = new NpgsqlCommand(
            """
            SELECT org_id
            FROM projects
            WHERE id = @project_id
                AND status = 'active';
            """,
            connection);
        projectCommand.Parameters.AddWithValue("project_id", scopeId);

        var result = await projectCommand.ExecuteScalarAsync(cancellationToken);
        return result is Guid orgId
            ? new AdminPermissionDriftScope("project", scopeId, orgId, scopeId)
            : throw new InvalidOperationException($"Active project {scopeId:D} was not found.");
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftOrganizationMembershipRecord>> ReadOrganizationMembershipsAsync(
        NpgsqlConnection connection,
        AdminPermissionDriftScope scope,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT org_id, principal_id, access_level, created_at
            FROM organization_memberships
            WHERE org_id = @org_id
            ORDER BY created_at, principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("org_id", scope.OrgId!.Value);

        var records = new List<AdminPermissionDriftOrganizationMembershipRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftOrganizationMembershipRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftProjectMembershipRecord>> ReadProjectMembershipsAsync(
        NpgsqlConnection connection,
        AdminPermissionDriftScope scope,
        CancellationToken cancellationToken)
    {
        var whereClause = scope.ProjectId.HasValue
            ? "project.id = @project_id"
            : "project.org_id = @org_id AND project.status = 'active'";
        await using var command = new NpgsqlCommand(
            $$"""
            SELECT
                membership.project_id,
                project.org_id,
                membership.principal_id,
                membership.access_level,
                membership.created_at
            FROM project_memberships AS membership
            INNER JOIN projects AS project
                ON project.id = membership.project_id
            WHERE {{whereClause}}
            ORDER BY membership.created_at, membership.principal_id;
            """,
            connection);
        AddScopeParameters(command, scope);

        var records = new List<AdminPermissionDriftProjectMembershipRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftProjectMembershipRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftRoleAssignmentRecord>> ReadRoleAssignmentsAsync(
        NpgsqlConnection connection,
        AdminPermissionDriftScope scope,
        CancellationToken cancellationToken)
    {
        var scopePredicate = scope.ProjectId.HasValue
            ? """
              assignment.scope_type = 'global'
              OR (assignment.scope_type = 'org' AND assignment.scope_id = @org_id)
              OR (assignment.scope_type = 'project' AND assignment.scope_id = @project_id)
              """
            : """
              assignment.scope_type = 'global'
              OR (assignment.scope_type = 'org' AND assignment.scope_id = @org_id)
              OR EXISTS (
                  SELECT 1
                  FROM projects AS project
                  WHERE project.id = assignment.scope_id
                      AND project.org_id = @org_id
                      AND project.status = 'active'
              )
              """;
        await using var command = new NpgsqlCommand(
            $$"""
            SELECT id, principal_id, role_id, scope_type, scope_id, created_at
            FROM role_assignments AS assignment
            WHERE {{scopePredicate}}
            ORDER BY created_at, principal_id;
            """,
            connection);
        AddScopeParameters(command, scope);

        var records = new List<AdminPermissionDriftRoleAssignmentRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftRoleAssignmentRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftNamespaceGrantRecord>> ReadNamespaceGrantsAsync(
        NpgsqlConnection connection,
        AdminPermissionDriftScope scope,
        string namespacePrefix,
        bool explicitNamespacePrefix,
        CancellationToken cancellationToken)
    {
        var predicate = scope.ProjectId.HasValue || explicitNamespacePrefix
            ? """
              (
                  grant_record.namespace_prefix = @namespace_prefix
                  OR left(@namespace_prefix, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                  OR left(grant_record.namespace_prefix, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
              )
              """
            : """
              (
                  grant_record.namespace_prefix = @namespace_prefix
                  OR left(grant_record.namespace_prefix, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
                  OR EXISTS (
                      SELECT 1
                      FROM projects AS project
                      WHERE project.org_id = @org_id
                          AND project.status = 'active'
                          AND (
                              grant_record.namespace_prefix = '/project/' || project.id::text
                              OR left(grant_record.namespace_prefix, length('/project/' || project.id::text || '/')) = '/project/' || project.id::text || '/'
                          )
                  )
              )
              """;
        await using var command = new NpgsqlCommand(
            $$"""
            SELECT id, principal_id, role_id, namespace_prefix, permission, created_at
            FROM memory_access_grants AS grant_record
            WHERE {{predicate}}
            ORDER BY created_at, id;
            """,
            connection);
        AddScopeParameters(command, scope);
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);

        var records = new List<AdminPermissionDriftNamespaceGrantRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftNamespaceGrantRecord(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftServiceAccountRecord>> ReadServiceAccountsAsync(
        NpgsqlConnection connection,
        AdminPermissionDriftScope scope,
        CancellationToken cancellationToken)
    {
        var predicate = scope.ProjectId.HasValue
            ? "account.owner_project_id = @project_id OR account.owner_org_id = @org_id"
            : """
              account.owner_org_id = @org_id
              OR EXISTS (
                  SELECT 1
                  FROM projects AS project
                  WHERE project.id = account.owner_project_id
                      AND project.org_id = @org_id
                      AND project.status = 'active'
              )
              """;
        await using var command = new NpgsqlCommand(
            $$"""
            SELECT
                account.principal_id,
                CASE WHEN account.owner_org_id IS NOT NULL THEN 'org' ELSE 'project' END AS owner_scope_type,
                COALESCE(account.owner_org_id, account.owner_project_id) AS owner_scope_id,
                account.owner_principal_id,
                account.allowed_auth_method,
                account.status,
                account.review_due_at,
                account.expires_at,
                account.created_at,
                account.updated_at
            FROM service_accounts AS account
            WHERE {{predicate}}
            ORDER BY account.created_at, account.principal_id;
            """,
            connection);
        AddScopeParameters(command, scope);

        var records = new List<AdminPermissionDriftServiceAccountRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftServiceAccountRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                ReadNullableDateTimeOffset(reader, 6),
                ReadNullableDateTimeOffset(reader, 7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftServiceCredentialRecord>> ReadServiceCredentialsAsync(
        NpgsqlConnection connection,
        Guid[] servicePrincipalIds,
        CancellationToken cancellationToken)
    {
        if (servicePrincipalIds.Length == 0)
        {
            return [];
        }

        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                service_principal_id,
                auth_method,
                status,
                review_due_at,
                expires_at,
                last_used_at,
                created_at,
                updated_at
            FROM service_account_credentials
            WHERE service_principal_id = ANY(@service_principal_ids)
            ORDER BY created_at, id;
            """,
            connection);
        command.Parameters.Add("service_principal_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = servicePrincipalIds;

        var records = new List<AdminPermissionDriftServiceCredentialRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftServiceCredentialRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                ReadNullableDateTimeOffset(reader, 4),
                ReadNullableDateTimeOffset(reader, 5),
                ReadNullableDateTimeOffset(reader, 6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetFieldValue<DateTimeOffset>(8)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftIdentityBindingRecord>> ReadIdentityBindingsAsync(
        NpgsqlConnection connection,
        IReadOnlySet<Guid> principalIds,
        CancellationToken cancellationToken)
    {
        if (principalIds.Count == 0)
        {
            return [];
        }

        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                principal_id,
                provider,
                issuer,
                status,
                last_seen_at,
                created_at,
                updated_at
            FROM identity_bindings
            WHERE principal_id = ANY(@principal_ids)
            ORDER BY created_at, id;
            """,
            connection);
        command.Parameters.Add("principal_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = principalIds.ToArray();

        var records = new List<AdminPermissionDriftIdentityBindingRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftIdentityBindingRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                HashText(reader.GetString(3)),
                reader.GetString(4),
                ReadNullableDateTimeOffset(reader, 5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminPermissionDriftPrincipalRecord>> ReadPrincipalsAsync(
        NpgsqlConnection connection,
        IReadOnlySet<Guid> principalIds,
        CancellationToken cancellationToken)
    {
        if (principalIds.Count == 0)
        {
            return [];
        }

        await using var command = new NpgsqlCommand(
            """
            SELECT id, principal_type, status, created_at, updated_at
            FROM principals
            WHERE id = ANY(@principal_ids)
            ORDER BY created_at, id;
            """,
            connection);
        command.Parameters.Add("principal_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = principalIds.ToArray();

        var records = new List<AdminPermissionDriftPrincipalRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminPermissionDriftPrincipalRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return records;
    }

    private async Task<IReadOnlyList<AdminPermissionDriftEffectiveAccessPreviewRecord>> BuildEffectiveAccessPreviewsAsync(
        AdminPermissionDriftScope scope,
        string namespacePrefix,
        IEnumerable<Guid> principalIds,
        CancellationToken cancellationToken)
    {
        var memoryScope = ToMemoryScope(scope);
        var records = new List<AdminPermissionDriftEffectiveAccessPreviewRecord>();
        foreach (var principalId in principalIds)
        {
            foreach (var permission in MemoryAccessPermissions.All)
            {
                var decision = await accessAuthorizer.PreviewAsync(
                    new MemoryAccessRequest(
                        principalId,
                        permission,
                        memoryScope,
                        namespacePrefix),
                    cancellationToken);

                records.Add(new AdminPermissionDriftEffectiveAccessPreviewRecord(
                    principalId,
                    permission,
                    scope.ScopeType,
                    scope.ScopeId,
                    namespacePrefix,
                    decision.Allowed,
                    decision.Reason ?? "Allowed by current membership, role, and namespace grant policy.",
                    nameof(IMemoryAccessAuthorizer)));
            }
        }

        return records;
    }

    private static IReadOnlyList<AdminPermissionDriftFindingRecord> BuildFindings(
        AdminPermissionDriftScope scope,
        string namespacePrefix,
        DateTimeOffset staleBefore,
        DateTimeOffset generatedAt,
        IReadOnlyList<AdminPermissionDriftPrincipalRecord> principals,
        IReadOnlyList<AdminPermissionDriftIdentityBindingRecord> identityBindings,
        IReadOnlyList<AdminPermissionDriftServiceAccountRecord> serviceAccounts,
        IReadOnlyList<AdminPermissionDriftServiceCredentialRecord> serviceCredentials,
        IReadOnlyList<AdminPermissionDriftOrganizationMembershipRecord> organizationMemberships,
        IReadOnlyList<AdminPermissionDriftProjectMembershipRecord> projectMemberships,
        IReadOnlyList<AdminPermissionDriftRoleAssignmentRecord> roleAssignments,
        IReadOnlyList<AdminPermissionDriftNamespaceGrantRecord> namespaceGrants,
        IReadOnlyList<AdminPermissionDriftEffectiveAccessPreviewRecord> effectiveAccessPreviews)
    {
        var findings = new List<AdminPermissionDriftFindingRecord>();

        foreach (var principal in principals.Where(principal => principal.Status != "active"))
        {
            AddFinding(
                findings,
                "high",
                "inactive_principal_has_access",
                "principal",
                principal.PrincipalId.ToString("D"),
                principal.PrincipalId,
                scope,
                namespacePrefix: null,
                $"Principal status is '{principal.Status}' but it still appears in scoped access records.",
                "Disable or remove memberships, role assignments, namespace grants, service credentials, and identity bindings for this principal.");
        }

        foreach (var binding in identityBindings)
        {
            if (binding.Status != "active")
            {
                AddFinding(
                    findings,
                    "medium",
                    "inactive_identity_binding",
                    "identity_binding",
                    binding.BindingId.ToString("D"),
                    binding.PrincipalId,
                    scope,
                    namespacePrefix: null,
                    $"Identity binding status is '{binding.Status}'.",
                    "Review whether the binding should be deleted or restored through the audited identity-binding process.");
            }
            else if (!binding.LastSeenAt.HasValue || binding.LastSeenAt.Value < staleBefore)
            {
                AddFinding(
                    findings,
                    "medium",
                    "stale_identity_binding",
                    "identity_binding",
                    binding.BindingId.ToString("D"),
                    binding.PrincipalId,
                    scope,
                    namespacePrefix: null,
                    "Active identity binding has not been seen inside the configured stale window.",
                    "Confirm the identity is still required or disable the binding.");
            }
        }

        foreach (var account in serviceAccounts)
        {
            if (account.Status != "active")
            {
                AddFinding(
                    findings,
                    "medium",
                    "inactive_service_account",
                    "service_account",
                    account.ServicePrincipalId.ToString("D"),
                    account.ServicePrincipalId,
                    scope,
                    namespacePrefix: null,
                    $"Service account status is '{account.Status}'.",
                    "Remove remaining namespace grants and credentials or restore the account through the audited lifecycle path.");
            }

            AddDueFinding(
                findings,
                account.ReviewDueAt,
                generatedAt,
                "service_account_review_due",
                "service_account",
                account.ServicePrincipalId.ToString("D"),
                account.ServicePrincipalId,
                scope,
                "Service account review is due or overdue.",
                "Complete service-account owner review and update the lifecycle record.");
            AddDueFinding(
                findings,
                account.ExpiresAt,
                generatedAt,
                "service_account_expired",
                "service_account",
                account.ServicePrincipalId.ToString("D"),
                account.ServicePrincipalId,
                scope,
                "Service account expiry is due or overdue.",
                "Rotate, renew, or disable the service account.");
        }

        foreach (var credential in serviceCredentials)
        {
            if (credential.Status != "active")
            {
                AddFinding(
                    findings,
                    "medium",
                    "inactive_service_credential",
                    "service_credential",
                    credential.CredentialId.ToString("D"),
                    credential.ServicePrincipalId,
                    scope,
                    namespacePrefix: null,
                    $"Service credential status is '{credential.Status}'.",
                    "Confirm the credential no longer appears in runtime secret configuration.");
            }

            AddDueFinding(
                findings,
                credential.ReviewDueAt,
                generatedAt,
                "service_credential_review_due",
                "service_credential",
                credential.CredentialId.ToString("D"),
                credential.ServicePrincipalId,
                scope,
                "Service credential review is due or overdue.",
                "Review or rotate the credential.");
            AddDueFinding(
                findings,
                credential.ExpiresAt,
                generatedAt,
                "service_credential_expired",
                "service_credential",
                credential.CredentialId.ToString("D"),
                credential.ServicePrincipalId,
                scope,
                "Service credential expiry is due or overdue.",
                "Rotate or disable the credential.");

            if (credential.Status == "active"
                && (!credential.LastUsedAt.HasValue || credential.LastUsedAt.Value < staleBefore))
            {
                AddFinding(
                    findings,
                    "medium",
                    "stale_service_credential",
                    "service_credential",
                    credential.CredentialId.ToString("D"),
                    credential.ServicePrincipalId,
                    scope,
                    namespacePrefix: null,
                    "Active service credential has not been used inside the configured stale window.",
                    "Confirm the credential is still required or disable it.");
            }
        }

        foreach (var membership in organizationMemberships.Where(membership => membership.AccessLevel is "admin" or "owner"))
        {
            AddFinding(
                findings,
                "high",
                "over_broad_org_membership",
                "organization_membership",
                $"{membership.OrgId:D}:{membership.PrincipalId:D}",
                membership.PrincipalId,
                scope,
                namespacePrefix: null,
                $"Organization membership grants '{membership.AccessLevel}' access.",
                "Confirm the principal still needs organization-level administration.");
        }

        foreach (var membership in projectMemberships.Where(membership => membership.AccessLevel == "admin"))
        {
            AddFinding(
                findings,
                "high",
                "over_broad_project_membership",
                "project_membership",
                $"{membership.ProjectId:D}:{membership.PrincipalId:D}",
                membership.PrincipalId,
                scope,
                namespacePrefix: null,
                "Project membership grants admin access.",
                "Confirm the principal still needs project-level administration.");
        }

        foreach (var assignment in roleAssignments.Where(assignment => assignment.ScopeType == "global"))
        {
            AddFinding(
                findings,
                "medium",
                "global_role_assignment",
                "role_assignment",
                assignment.AssignmentId.ToString("D"),
                assignment.PrincipalId,
                scope,
                namespacePrefix: null,
                $"Role '{assignment.RoleId}' is assigned globally.",
                "Prefer org or project role assignments for pilot and production operations.");
        }

        foreach (var grant in namespaceGrants)
        {
            if (grant.Permission == MemoryAccessPermissions.Admin)
            {
                AddFinding(
                    findings,
                    "high",
                    "over_broad_namespace_admin_grant",
                    "memory_access_grant",
                    grant.GrantId.ToString("D"),
                    grant.PrincipalId,
                    scope,
                    grant.NamespacePrefix,
                    "Namespace grant allows admin access.",
                    "Confirm the grant is required or narrow it to review/write/read.");
            }

            if (string.Equals(grant.NamespacePrefix, namespacePrefix, StringComparison.Ordinal))
            {
                AddFinding(
                    findings,
                    "medium",
                    "broad_namespace_prefix",
                    "memory_access_grant",
                    grant.GrantId.ToString("D"),
                    grant.PrincipalId,
                    scope,
                    grant.NamespacePrefix,
                    "Namespace grant applies to the full reported namespace prefix.",
                    "Prefer the narrowest namespace prefix that supports the workflow.");
            }
        }

        foreach (var preview in effectiveAccessPreviews.Where(preview => preview.Allowed && preview.Permission == MemoryAccessPermissions.Admin))
        {
            AddFinding(
                findings,
                "high",
                "effective_admin_access",
                "effective_access_preview",
                $"{preview.PrincipalId:D}:{preview.ScopeType}:{preview.ScopeId:D}:{preview.NamespacePrefix}",
                preview.PrincipalId,
                scope,
                preview.NamespacePrefix,
                "Effective-access preview allows admin permission.",
                "Review memberships, role assignments, and namespace grants that produce admin access.");
        }

        return findings
            .OrderBy(FindingSeverityRank)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ThenBy(finding => finding.ResourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<Guid> CollectCandidatePrincipalIds(
        IReadOnlyList<AdminPermissionDriftOrganizationMembershipRecord> organizationMemberships,
        IReadOnlyList<AdminPermissionDriftProjectMembershipRecord> projectMemberships,
        IReadOnlyList<AdminPermissionDriftRoleAssignmentRecord> roleAssignments,
        IReadOnlyList<AdminPermissionDriftNamespaceGrantRecord> namespaceGrants,
        IReadOnlyList<AdminPermissionDriftServiceAccountRecord> serviceAccounts,
        IReadOnlyList<AdminPermissionDriftServiceCredentialRecord> serviceCredentials)
    {
        var principalIds = new HashSet<Guid>();

        principalIds.UnionWith(organizationMemberships.Select(record => record.PrincipalId));
        principalIds.UnionWith(projectMemberships.Select(record => record.PrincipalId));
        principalIds.UnionWith(roleAssignments.Select(record => record.PrincipalId));
        principalIds.UnionWith(namespaceGrants.Where(record => record.PrincipalId.HasValue).Select(record => record.PrincipalId!.Value));
        principalIds.UnionWith(serviceAccounts.Select(record => record.ServicePrincipalId));
        principalIds.UnionWith(serviceAccounts.Where(record => record.OwnerPrincipalId.HasValue).Select(record => record.OwnerPrincipalId!.Value));
        principalIds.UnionWith(serviceCredentials.Select(record => record.ServicePrincipalId));

        return principalIds;
    }

    private static void AddFinding(
        ICollection<AdminPermissionDriftFindingRecord> findings,
        string severity,
        string code,
        string resourceType,
        string resourceId,
        Guid? principalId,
        AdminPermissionDriftScope scope,
        string? namespacePrefix,
        string detail,
        string recommendedAction)
    {
        findings.Add(new AdminPermissionDriftFindingRecord(
            severity,
            code,
            resourceType,
            resourceId,
            principalId,
            scope.ScopeType,
            scope.ScopeId.ToString("D"),
            namespacePrefix,
            detail,
            recommendedAction));
    }

    private static void AddDueFinding(
        ICollection<AdminPermissionDriftFindingRecord> findings,
        DateTimeOffset? dueAt,
        DateTimeOffset generatedAt,
        string code,
        string resourceType,
        string resourceId,
        Guid? principalId,
        AdminPermissionDriftScope scope,
        string detail,
        string recommendedAction)
    {
        if (!dueAt.HasValue || dueAt.Value > generatedAt)
        {
            return;
        }

        AddFinding(
            findings,
            "high",
            code,
            resourceType,
            resourceId,
            principalId,
            scope,
            namespacePrefix: null,
            detail,
            recommendedAction);
    }

    private static int FindingSeverityRank(AdminPermissionDriftFindingRecord finding)
    {
        return finding.Severity switch
        {
            "high" => 0,
            "medium" => 1,
            "low" => 2,
            _ => 3
        };
    }

    private static MemoryScopeResolution ToMemoryScope(AdminPermissionDriftScope scope)
    {
        return scope.ScopeType switch
        {
            "org" => new MemoryScopeResolution("org", scope.ScopeId.ToString("D"), OrgId: scope.OrgId),
            "project" => new MemoryScopeResolution("project", scope.ScopeId.ToString("D"), OrgId: scope.OrgId, ProjectId: scope.ProjectId),
            _ => throw new InvalidOperationException($"Unsupported permission-drift scope '{scope.ScopeType}'.")
        };
    }

    private static void AddScopeParameters(NpgsqlCommand command, AdminPermissionDriftScope scope)
    {
        command.Parameters.AddWithValue("org_id", scope.OrgId!.Value);
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            scope.ProjectId.HasValue ? scope.ProjectId.Value : DBNull.Value;
    }

    private static string? NormalizeNamespacePrefix(string? namespacePrefix)
    {
        if (string.IsNullOrWhiteSpace(namespacePrefix))
        {
            return null;
        }

        var normalized = namespacePrefix.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Namespace prefix must start with '/'.");
        }

        if (normalized == "/")
        {
            throw new ArgumentException("Namespace prefix must be narrower than '/'.");
        }

        return normalized.TrimEnd('/');
    }

    private static string DefaultNamespacePrefix(AdminPermissionDriftScope scope)
    {
        return scope.ProjectId.HasValue
            ? $"/project/{scope.ProjectId.Value:D}"
            : $"/org/{scope.OrgId!.Value:D}";
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
