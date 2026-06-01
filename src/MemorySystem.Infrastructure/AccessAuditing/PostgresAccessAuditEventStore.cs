using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.AccessAuditing;

public sealed class PostgresAccessAuditEventStore(NpgsqlDataSource dataSource) : IAccessAuditEventStore
{
    private static readonly IReadOnlySet<string> PrincipalTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "human",
        "agent",
        "service"
    };

    private static readonly IReadOnlySet<string> AuthMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthenticationMethods.ApiKey,
        AuthenticationMethods.Oidc,
        AuthenticationMethods.ServiceAccount
    };

    private static readonly IReadOnlySet<string> ForbiddenMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "rawPayload",
        "raw_payload",
        "payload",
        "content",
        "sourcePayload",
        "source_payload",
        "memoryText",
        "memory_text",
        "memoryObject",
        "memory_object",
        "eventContent",
        "event_content"
    };

    public async Task<AccessAuditEventRecord> RecordAsync(
        AccessAuditEventCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        _ = Normalize(command);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await RecordAsync(command, connection, transaction: null, cancellationToken);
    }

    public async Task<AccessAuditEventRecord> RecordAsync(
        AccessAuditEventCommand command,
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(connection);

        var normalized = Normalize(command);
        var id = Guid.NewGuid();
        var metadataJson = JsonSerializer.Serialize(normalized.Metadata);

        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO access_audit_events (
                id,
                action_type,
                outcome,
                actor_principal_id,
                target_principal_id,
                principal_type,
                auth_method,
                credential_id,
                identity_binding_id,
                scope_type,
                scope_id,
                role_id,
                namespace_prefix,
                permission,
                resource_type,
                resource_id,
                reason_code,
                request_method,
                request_path,
                correlation_id,
                audit_metadata
            )
            VALUES (
                @id,
                @action_type,
                @outcome,
                @actor_principal_id,
                @target_principal_id,
                @principal_type,
                @auth_method,
                @credential_id,
                @identity_binding_id,
                @scope_type,
                @scope_id,
                @role_id,
                @namespace_prefix,
                @permission,
                @resource_type,
                @resource_id,
                @reason_code,
                @request_method,
                @request_path,
                @correlation_id,
                @audit_metadata
            )
            RETURNING occurred_at;
            """,
            connection,
            transaction);

        sql.Parameters.AddWithValue("id", id);
        sql.Parameters.AddWithValue("action_type", normalized.ActionType);
        sql.Parameters.AddWithValue("outcome", normalized.Outcome);
        sql.Parameters.Add("actor_principal_id", NpgsqlDbType.Uuid).Value =
            normalized.ActorPrincipalId.HasValue ? normalized.ActorPrincipalId.Value : DBNull.Value;
        sql.Parameters.Add("target_principal_id", NpgsqlDbType.Uuid).Value =
            normalized.TargetPrincipalId.HasValue ? normalized.TargetPrincipalId.Value : DBNull.Value;
        AddOptionalTextParameter(sql, "principal_type", normalized.PrincipalType);
        AddOptionalTextParameter(sql, "auth_method", normalized.AuthMethod);
        AddOptionalTextParameter(sql, "credential_id", normalized.CredentialId);
        sql.Parameters.Add("identity_binding_id", NpgsqlDbType.Uuid).Value =
            normalized.IdentityBindingId.HasValue ? normalized.IdentityBindingId.Value : DBNull.Value;
        AddOptionalTextParameter(sql, "scope_type", normalized.ScopeType);
        AddOptionalTextParameter(sql, "scope_id", normalized.ScopeId);
        AddOptionalTextParameter(sql, "role_id", normalized.RoleId);
        AddOptionalTextParameter(sql, "namespace_prefix", normalized.NamespacePrefix);
        AddOptionalTextParameter(sql, "permission", normalized.Permission);
        AddOptionalTextParameter(sql, "resource_type", normalized.ResourceType);
        AddOptionalTextParameter(sql, "resource_id", normalized.ResourceId);
        AddOptionalTextParameter(sql, "reason_code", normalized.ReasonCode);
        AddOptionalTextParameter(sql, "request_method", normalized.RequestMethod);
        AddOptionalTextParameter(sql, "request_path", normalized.RequestPath);
        AddOptionalTextParameter(sql, "correlation_id", normalized.CorrelationId);
        sql.Parameters.Add("audit_metadata", NpgsqlDbType.Jsonb).Value = metadataJson;

        var occurredAt = await sql.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Access audit event insert did not return an occurrence timestamp.");

        return new AccessAuditEventRecord(
            id,
            normalized.ActionType,
            normalized.Outcome,
            normalized.ActorPrincipalId,
            normalized.TargetPrincipalId,
            normalized.PrincipalType,
            normalized.AuthMethod,
            normalized.CredentialId,
            normalized.IdentityBindingId,
            normalized.ScopeType,
            normalized.ScopeId,
            normalized.RoleId,
            normalized.NamespacePrefix,
            normalized.Permission,
            normalized.ResourceType,
            normalized.ResourceId,
            normalized.ReasonCode,
            normalized.RequestMethod,
            normalized.RequestPath,
            normalized.CorrelationId,
            normalized.Metadata,
            ToDateTimeOffset(occurredAt));
    }

    private static NormalizedAccessAuditEvent Normalize(AccessAuditEventCommand command)
    {
        var actionType = NormalizeRequiredToken(command.ActionType, "Action type").ToLowerInvariant();
        if (!AccessAuditActionTypes.IsSupported(actionType))
        {
            throw new ArgumentException("Access audit action type is not supported.", nameof(command));
        }

        var outcome = NormalizeRequiredToken(command.Outcome, "Outcome").ToLowerInvariant();
        if (!AccessAuditOutcomes.IsSupported(outcome))
        {
            throw new ArgumentException("Access audit outcome is not supported.", nameof(command));
        }

        var actorPrincipalId = NormalizeOptionalGuid(command.ActorPrincipalId, "Actor principal id");
        var targetPrincipalId = NormalizeOptionalGuid(command.TargetPrincipalId, "Target principal id");
        var identityBindingId = NormalizeOptionalGuid(command.IdentityBindingId, "Identity binding id");
        var principalType = NormalizeOptionalAllowedToken(command.PrincipalType, PrincipalTypes, "Principal type");
        var authMethod = NormalizeOptionalAllowedToken(command.AuthMethod, AuthMethods, "Authentication method");
        var scopeType = NormalizeOptionalScopeType(command.ScopeType);
        var scopeId = NormalizeOptionalToken(command.ScopeId, "Scope id");

        if ((scopeType is null) != (scopeId is null))
        {
            throw new ArgumentException("Scope type and scope id must be provided together.", nameof(command));
        }

        var roleId = NormalizeOptionalRoleId(command.RoleId);
        var namespacePrefix = NormalizeOptionalToken(command.NamespacePrefix, "Namespace prefix");
        if (namespacePrefix is not null && !namespacePrefix.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Namespace prefix must start with '/'.", nameof(command));
        }

        var permission = NormalizeOptionalAllowedToken(command.Permission, MemoryAccessPermissions.All, "Permission");
        var requestMethod = NormalizeOptionalToken(command.RequestMethod, "Request method")?.ToUpperInvariant();
        var requestPath = NormalizeOptionalToken(command.RequestPath, "Request path");
        if (requestPath is not null && !requestPath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Request path must start with '/'.", nameof(command));
        }

        var metadata = NormalizeMetadata(command.Metadata);

        return new NormalizedAccessAuditEvent(
            actionType,
            outcome,
            actorPrincipalId,
            targetPrincipalId,
            principalType,
            authMethod,
            NormalizeOptionalToken(command.CredentialId, "Credential id"),
            identityBindingId,
            scopeType,
            scopeId,
            roleId,
            namespacePrefix,
            permission,
            NormalizeOptionalToken(command.ResourceType, "Resource type"),
            NormalizeOptionalToken(command.ResourceId, "Resource id"),
            NormalizeOptionalToken(command.ReasonCode, "Reason code"),
            requestMethod,
            requestPath,
            NormalizeOptionalToken(command.CorrelationId, "Correlation id"),
            metadata);
    }

    private static Guid? NormalizeOptionalGuid(Guid? value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is invalid.");
        }

        return value;
    }

    private static string NormalizeRequiredToken(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{fieldName} is required.")
            : normalized;
    }

    private static string? NormalizeOptionalToken(string? value, string fieldName)
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

    private static string? NormalizeOptionalAllowedToken(
        string? value,
        IReadOnlySet<string> allowedValues,
        string fieldName)
    {
        var normalized = NormalizeOptionalToken(value, fieldName)?.ToLowerInvariant();
        if (normalized is null)
        {
            return null;
        }

        if (!allowedValues.Contains(normalized))
        {
            throw new ArgumentException($"{fieldName} is not supported.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalScopeType(string? value)
    {
        var normalized = NormalizeOptionalToken(value, "Scope type");
        if (normalized is null)
        {
            return null;
        }

        if (!MemoryScopeType.TryNormalize(normalized, out var scopeType, out _))
        {
            throw new ArgumentException("Scope type is not supported.");
        }

        return scopeType!.Value;
    }

    private static string? NormalizeOptionalRoleId(string? value)
    {
        var normalized = NormalizeOptionalToken(value, "Role id");
        if (normalized is null)
        {
            return null;
        }

        if (!MemoryRoleId.TryNormalize(normalized, out var roleId, out _))
        {
            throw new ArgumentException("Role id is not supported.");
        }

        return roleId!.Value;
    }

    private static IReadOnlyDictionary<string, string?> NormalizeMetadata(IReadOnlyDictionary<string, string?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Access audit metadata keys must not be blank.");
            }

            var normalizedKey = key.Trim();
            if (ForbiddenMetadataKeys.Contains(normalizedKey))
            {
                throw new ArgumentException("Access audit metadata must not contain raw payload fields.");
            }

            normalized[normalizedKey] = value;
        }

        return normalized;
    }

    private static void AddOptionalTextParameter(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value =
            value is null ? DBNull.Value : value;
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

    private sealed record NormalizedAccessAuditEvent(
        string ActionType,
        string Outcome,
        Guid? ActorPrincipalId,
        Guid? TargetPrincipalId,
        string? PrincipalType,
        string? AuthMethod,
        string? CredentialId,
        Guid? IdentityBindingId,
        string? ScopeType,
        string? ScopeId,
        string? RoleId,
        string? NamespacePrefix,
        string? Permission,
        string? ResourceType,
        string? ResourceId,
        string? ReasonCode,
        string? RequestMethod,
        string? RequestPath,
        string? CorrelationId,
        IReadOnlyDictionary<string, string?> Metadata);
}
