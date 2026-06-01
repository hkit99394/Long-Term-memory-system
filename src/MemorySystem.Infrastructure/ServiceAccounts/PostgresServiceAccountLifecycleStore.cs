using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using MemorySystem.Application.ServiceAccounts;
using MemorySystem.Infrastructure.AccessAuditing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.ServiceAccounts;

public sealed class PostgresServiceAccountLifecycleStore(
    NpgsqlDataSource dataSource,
    PostgresAccessAuditEventStore accessAuditEventStore) : IServiceAccountLifecycleStore
{
    private static readonly IReadOnlySet<string> AllowedAuthMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthenticationMethods.ApiKey,
        AuthenticationMethods.Oidc,
        AuthenticationMethods.ServiceAccount
    };

    public async Task<ServiceAccountProfileRecord> UpsertProfileAsync(
        ServiceAccountProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateProfile(command);
        var ownerScope = NormalizeOwnerScope(command.OwnerScopeType, command.OwnerScopeId);
        var allowedAuthMethod = NormalizeAuthMethod(command.AllowedAuthMethod);
        var adminContact = NormalizeOptionalText(command.AdminContact, "Admin contact");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO service_accounts (
                principal_id,
                owner_org_id,
                owner_project_id,
                owner_principal_id,
                admin_contact,
                allowed_auth_method,
                review_due_at,
                expires_at,
                created_by_principal_id
            )
            VALUES (
                @principal_id,
                @owner_org_id,
                @owner_project_id,
                @owner_principal_id,
                @admin_contact,
                @allowed_auth_method,
                @review_due_at,
                @expires_at,
                @created_by_principal_id
            )
            ON CONFLICT (principal_id)
            DO UPDATE SET
                owner_org_id = EXCLUDED.owner_org_id,
                owner_project_id = EXCLUDED.owner_project_id,
                owner_principal_id = EXCLUDED.owner_principal_id,
                admin_contact = EXCLUDED.admin_contact,
                allowed_auth_method = EXCLUDED.allowed_auth_method,
                review_due_at = EXCLUDED.review_due_at,
                expires_at = EXCLUDED.expires_at
            RETURNING
                principal_id,
                owner_org_id,
                owner_project_id,
                owner_principal_id,
                admin_contact,
                allowed_auth_method,
                status,
                review_due_at,
                expires_at,
                created_at,
                updated_at;
            """,
            connection);

        sql.Parameters.AddWithValue("principal_id", command.ServicePrincipalId);
        sql.Parameters.Add("owner_org_id", NpgsqlDbType.Uuid).Value =
            ownerScope.ScopeType == "org" ? ownerScope.ScopeId : DBNull.Value;
        sql.Parameters.Add("owner_project_id", NpgsqlDbType.Uuid).Value =
            ownerScope.ScopeType == "project" ? ownerScope.ScopeId : DBNull.Value;
        sql.Parameters.Add("owner_principal_id", NpgsqlDbType.Uuid).Value =
            command.OwnerPrincipalId.HasValue ? command.OwnerPrincipalId.Value : DBNull.Value;
        sql.Parameters.Add("admin_contact", NpgsqlDbType.Text).Value =
            adminContact is null ? DBNull.Value : adminContact;
        sql.Parameters.AddWithValue("allowed_auth_method", allowedAuthMethod);
        sql.Parameters.Add("review_due_at", NpgsqlDbType.TimestampTz).Value =
            command.ReviewDueAt.HasValue ? command.ReviewDueAt.Value : DBNull.Value;
        sql.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value =
            command.ExpiresAt.HasValue ? command.ExpiresAt.Value : DBNull.Value;
        sql.Parameters.AddWithValue("created_by_principal_id", command.ActorPrincipalId);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Service account profile write did not return a record.");
        }

        return ReadProfile(reader);
    }

    public async Task<ServiceAccountCredentialRecord> CreateCredentialAsync(
        ServiceAccountCredentialCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateCredential(command);
        var credentialId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var credential = await InsertCredentialAsync(
            connection,
            transaction,
            credentialId,
            command.ServicePrincipalId,
            command.ActorPrincipalId,
            command.CredentialLabel,
            command.AuthMethod,
            command.CredentialFingerprint,
            command.ReviewDueAt,
            command.ExpiresAt,
            rotatedFromCredentialId: null,
            cancellationToken);

        await RecordCredentialAuditAsync(
            connection,
            transaction,
            command.ActorPrincipalId,
            command.ServicePrincipalId,
            credential.CredentialId,
            credential.AuthMethod,
            "created",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return credential;
    }

    public async Task<ServiceAccountCredentialRecord> RotateCredentialAsync(
        ServiceAccountCredentialRotationCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateRotation(command);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid servicePrincipalId;
        string authMethod;
        await using (var update = new NpgsqlCommand(
            """
            UPDATE service_account_credentials
            SET status = 'rotated',
                disabled_by_principal_id = @actor_principal_id,
                disabled_at = now(),
                disable_reason = 'rotated'
            WHERE id = @credential_id
                AND status = 'active'
            RETURNING service_principal_id, auth_method;
            """,
            connection,
            transaction))
        {
            update.Parameters.AddWithValue("credential_id", command.CurrentCredentialId);
            update.Parameters.AddWithValue("actor_principal_id", command.ActorPrincipalId);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Active service account credential was not found.");
            }

            servicePrincipalId = reader.GetGuid(0);
            authMethod = reader.GetString(1);
        }

        var replacementId = Guid.NewGuid();
        ServiceAccountCredentialRecord replacement;
        await using (var insert = new NpgsqlCommand(InsertCredentialSql, connection, transaction))
        {
            AddCredentialParameters(
                insert,
                replacementId,
                servicePrincipalId,
                command.ActorPrincipalId,
                command.ReplacementCredentialLabel,
                authMethod,
                command.ReplacementCredentialFingerprint,
                command.ReviewDueAt,
                command.ExpiresAt,
                command.CurrentCredentialId);

            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Service account credential rotation did not return a replacement credential.");
            }

            replacement = ReadCredential(reader);
        }

        await RecordCredentialAuditAsync(
            connection,
            transaction,
            command.ActorPrincipalId,
            servicePrincipalId,
            replacement.CredentialId,
            replacement.AuthMethod,
            "rotated",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return replacement;
    }

    public async Task<ServiceAccountCredentialRecord> DisableCredentialAsync(
        ServiceAccountCredentialDisableCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateDisable(command);
        var reason = NormalizeRequiredText(command.Reason, "Disable reason");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        ServiceAccountCredentialRecord credential;
        await using (var sql = new NpgsqlCommand(
            """
            UPDATE service_account_credentials
            SET status = 'disabled',
                disabled_by_principal_id = @actor_principal_id,
                disabled_at = now(),
                disable_reason = @disable_reason
            WHERE id = @credential_id
                AND status = 'active'
            RETURNING
                id,
                service_principal_id,
                credential_label,
                auth_method,
                credential_fingerprint,
                status,
                rotated_from_credential_id,
                review_due_at,
                expires_at,
                disabled_at,
                disable_reason,
                created_at,
                updated_at;
            """,
            connection,
            transaction))
        {
            sql.Parameters.AddWithValue("credential_id", command.CredentialId);
            sql.Parameters.AddWithValue("actor_principal_id", command.ActorPrincipalId);
            sql.Parameters.AddWithValue("disable_reason", reason);

            await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Active service account credential was not found.");
            }

            credential = ReadCredential(reader);
        }

        await RecordCredentialAuditAsync(
            connection,
            transaction,
            command.ActorPrincipalId,
            credential.ServicePrincipalId,
            credential.CredentialId,
            credential.AuthMethod,
            "disabled",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return credential;
    }

    public async Task<ServiceAccountNamespaceGrantRecord> GrantNamespaceAsync(
        ServiceAccountNamespaceGrantCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateNamespaceGrant(command);
        var namespacePrefix = NormalizeRequiredText(command.NamespacePrefix, "Namespace prefix");
        var permission = NormalizePermission(command.Permission);
        var grantId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO memory_access_grants (
                id,
                principal_id,
                namespace_prefix,
                permission
            )
            VALUES (
                @grant_id,
                @service_principal_id,
                @namespace_prefix,
                @permission
            )
            RETURNING created_at;
            """,
            connection,
            transaction);
        sql.Parameters.AddWithValue("grant_id", grantId);
        sql.Parameters.AddWithValue("service_principal_id", command.ServicePrincipalId);
        sql.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        sql.Parameters.AddWithValue("permission", permission);

        var createdAt = await sql.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Service account namespace grant insert did not return a creation timestamp.");

        await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.NamespaceGrantChange,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                TargetPrincipalId: command.ServicePrincipalId,
                PrincipalType: "service",
                NamespacePrefix: namespacePrefix,
                Permission: permission,
                ResourceType: "memory_access_grant",
                ResourceId: grantId.ToString("D"),
                Metadata: new Dictionary<string, string?> { ["operation"] = "created" }),
            connection,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ServiceAccountNamespaceGrantRecord(
            grantId,
            command.ServicePrincipalId,
            namespacePrefix,
            permission,
            ToDateTimeOffset(createdAt));
    }

    private static async Task<ServiceAccountCredentialRecord> InsertCredentialAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid credentialId,
        Guid servicePrincipalId,
        Guid actorPrincipalId,
        string credentialLabel,
        string authMethod,
        string credentialFingerprint,
        DateTimeOffset? reviewDueAt,
        DateTimeOffset? expiresAt,
        Guid? rotatedFromCredentialId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(InsertCredentialSql, connection, transaction);
        AddCredentialParameters(
            sql,
            credentialId,
            servicePrincipalId,
            actorPrincipalId,
            credentialLabel,
            authMethod,
            credentialFingerprint,
            reviewDueAt,
            expiresAt,
            rotatedFromCredentialId);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Service account credential write did not return a record.");
        }

        return ReadCredential(reader);
    }

    private async Task RecordCredentialAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorPrincipalId,
        Guid servicePrincipalId,
        Guid credentialId,
        string authMethod,
        string operation,
        CancellationToken cancellationToken)
    {
        await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.ServiceCredentialChange,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: actorPrincipalId,
                TargetPrincipalId: servicePrincipalId,
                PrincipalType: "service",
                AuthMethod: authMethod,
                CredentialId: credentialId.ToString("D"),
                ResourceType: "service_credential",
                ResourceId: credentialId.ToString("D"),
                Metadata: new Dictionary<string, string?> { ["operation"] = operation }),
            connection,
            transaction,
            cancellationToken);
    }

    private static void AddCredentialParameters(
        NpgsqlCommand command,
        Guid credentialId,
        Guid servicePrincipalId,
        Guid actorPrincipalId,
        string credentialLabel,
        string authMethod,
        string credentialFingerprint,
        DateTimeOffset? reviewDueAt,
        DateTimeOffset? expiresAt,
        Guid? rotatedFromCredentialId)
    {
        command.Parameters.AddWithValue("credential_id", credentialId);
        command.Parameters.AddWithValue("service_principal_id", servicePrincipalId);
        command.Parameters.AddWithValue("credential_label", NormalizeRequiredText(credentialLabel, "Credential label"));
        command.Parameters.AddWithValue("auth_method", NormalizeAuthMethod(authMethod));
        command.Parameters.AddWithValue("credential_fingerprint", NormalizeRequiredText(credentialFingerprint, "Credential fingerprint"));
        command.Parameters.AddWithValue("created_by_principal_id", actorPrincipalId);
        command.Parameters.Add("review_due_at", NpgsqlDbType.TimestampTz).Value =
            reviewDueAt.HasValue ? reviewDueAt.Value : DBNull.Value;
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value =
            expiresAt.HasValue ? expiresAt.Value : DBNull.Value;
        command.Parameters.Add("rotated_from_credential_id", NpgsqlDbType.Uuid).Value =
            rotatedFromCredentialId.HasValue ? rotatedFromCredentialId.Value : DBNull.Value;
    }

    private static ServiceAccountProfileRecord ReadProfile(NpgsqlDataReader reader)
    {
        var ownerOrgId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
        var ownerProjectId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);

        return new ServiceAccountProfileRecord(
            reader.GetGuid(0),
            ownerOrgId.HasValue ? "org" : "project",
            ownerOrgId ?? ownerProjectId!.Value,
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetFieldValue<DateTimeOffset>(10));
    }

    private static ServiceAccountCredentialRecord ReadCredential(NpgsqlDataReader reader)
    {
        return new ServiceAccountCredentialRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11),
            reader.GetFieldValue<DateTimeOffset>(12));
    }

    private static void ValidateProfile(ServiceAccountProfileCommand command)
    {
        ValidateId(command.ServicePrincipalId, "Service principal id");
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        if (command.OwnerPrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Owner principal id is invalid.", nameof(command));
        }

        if (!command.OwnerPrincipalId.HasValue && string.IsNullOrWhiteSpace(command.AdminContact))
        {
            throw new ArgumentException("A service account owner principal or admin contact is required.", nameof(command));
        }

        ValidateReviewOrExpiry(command.ReviewDueAt, command.ExpiresAt);
    }

    private static void ValidateCredential(ServiceAccountCredentialCreateCommand command)
    {
        ValidateId(command.ServicePrincipalId, "Service principal id");
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateReviewOrExpiry(command.ReviewDueAt, command.ExpiresAt);
    }

    private static void ValidateRotation(ServiceAccountCredentialRotationCommand command)
    {
        ValidateId(command.CurrentCredentialId, "Current credential id");
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateReviewOrExpiry(command.ReviewDueAt, command.ExpiresAt);
        NormalizeRequiredText(command.ReplacementCredentialLabel, "Replacement credential label");
        NormalizeRequiredText(command.ReplacementCredentialFingerprint, "Replacement credential fingerprint");
    }

    private static void ValidateDisable(ServiceAccountCredentialDisableCommand command)
    {
        ValidateId(command.CredentialId, "Credential id");
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        NormalizeRequiredText(command.Reason, "Disable reason");
    }

    private static void ValidateNamespaceGrant(ServiceAccountNamespaceGrantCommand command)
    {
        ValidateId(command.ServicePrincipalId, "Service principal id");
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        var namespacePrefix = NormalizeRequiredText(command.NamespacePrefix, "Namespace prefix");
        var permission = NormalizePermission(command.Permission);

        if (permission == MemoryAccessPermissions.Admin)
        {
            throw new ArgumentException("Service account grants must not use admin permission.", nameof(command));
        }

        if (namespacePrefix is "/" or "/global")
        {
            throw new ArgumentException("Service account grants must use a narrow namespace prefix.", nameof(command));
        }
    }

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private static void ValidateReviewOrExpiry(DateTimeOffset? reviewDueAt, DateTimeOffset? expiresAt)
    {
        if (!reviewDueAt.HasValue && !expiresAt.HasValue)
        {
            throw new ArgumentException("A review due date or expiry date is required.");
        }

        if (reviewDueAt.HasValue && expiresAt.HasValue && expiresAt.Value < reviewDueAt.Value)
        {
            throw new ArgumentException("Expiry date must be after the review due date.");
        }
    }

    private static (string ScopeType, Guid ScopeId) NormalizeOwnerScope(string ownerScopeType, Guid ownerScopeId)
    {
        var normalizedScopeType = NormalizeRequiredText(ownerScopeType, "Owner scope type").ToLowerInvariant();
        if (normalizedScopeType is not ("org" or "project"))
        {
            throw new ArgumentException("Service account owner scope must be org or project.");
        }

        ValidateId(ownerScopeId, "Owner scope id");
        return (normalizedScopeType, ownerScopeId);
    }

    private static string NormalizeAuthMethod(string authMethod)
    {
        var normalized = NormalizeRequiredText(authMethod, "Authentication method").ToLowerInvariant();
        if (!AllowedAuthMethods.Contains(normalized))
        {
            throw new ArgumentException("Service account authentication method is not supported.");
        }

        return normalized;
    }

    private static string NormalizePermission(string permission)
    {
        var normalized = NormalizeRequiredText(permission, "Permission").ToLowerInvariant();
        if (!MemoryAccessPermissions.All.Contains(normalized))
        {
            throw new ArgumentException("Service account grant permission is not supported.");
        }

        return normalized;
    }

    private static string NormalizeRequiredText(string value, string fieldName)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{fieldName} is required.")
            : normalized;
    }

    private static string? NormalizeOptionalText(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (normalized is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{fieldName} must not be blank.")
            : normalized;
    }

    private static DateTimeOffset ToDateTimeOffset(object value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Unexpected timestamp value returned from PostgreSQL.")
        };
    }

    private const string InsertCredentialSql =
        """
        INSERT INTO service_account_credentials (
            id,
            service_principal_id,
            credential_label,
            auth_method,
            credential_fingerprint,
            review_due_at,
            expires_at,
            rotated_from_credential_id,
            created_by_principal_id
        )
        VALUES (
            @credential_id,
            @service_principal_id,
            @credential_label,
            @auth_method,
            @credential_fingerprint,
            @review_due_at,
            @expires_at,
            @rotated_from_credential_id,
            @created_by_principal_id
        )
        RETURNING
            id,
            service_principal_id,
            credential_label,
            auth_method,
            credential_fingerprint,
            status,
            rotated_from_credential_id,
            review_due_at,
            expires_at,
            disabled_at,
            disable_reason,
            created_at,
            updated_at;
        """;
}
