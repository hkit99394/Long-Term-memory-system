using MemorySystem.Application.Authentication;
using Npgsql;

namespace MemorySystem.Infrastructure.Authentication;

public sealed class PostgresIdentityBindingStore(NpgsqlDataSource dataSource) : IIdentityBindingStore
{
    public async Task<IdentityBindingPrincipal?> FindActiveAsync(
        IdentityBindingLookup lookup,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                binding.id,
                binding.principal_id,
                principal.principal_type,
                principal.display_name,
                binding.provider,
                binding.issuer,
                binding.subject,
                binding.external_display_name,
                binding.external_email,
                binding.external_tenant_id,
                binding.created_at,
                binding.updated_at,
                binding.last_seen_at
            FROM identity_bindings AS binding
            INNER JOIN principals AS principal
                ON principal.id = binding.principal_id
            WHERE binding.provider = @provider
                AND binding.issuer = @issuer
                AND binding.subject = @subject
                AND binding.status = 'active'
                AND principal.status = 'active'
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("provider", lookup.Provider);
        command.Parameters.AddWithValue("issuer", lookup.Issuer);
        command.Parameters.AddWithValue("subject", lookup.Subject);

        IdentityBindingPrincipal binding;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            binding = new IdentityBindingPrincipal(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetFieldValue<DateTimeOffset>(10),
                reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12));
        }

        var seen = await MarkSeenAsync(connection, binding.BindingId, cancellationToken);

        return binding with
        {
            UpdatedAt = seen.UpdatedAt,
            LastSeenAt = seen.LastSeenAt
        };
    }

    private static async Task<(DateTimeOffset UpdatedAt, DateTimeOffset LastSeenAt)> MarkSeenAsync(
        NpgsqlConnection connection,
        Guid bindingId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE identity_bindings
            SET last_seen_at = now()
            WHERE id = @binding_id
            RETURNING updated_at, last_seen_at;
            """,
            connection);
        command.Parameters.AddWithValue("binding_id", bindingId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Identity binding last-seen update did not return a record.");
        }

        return (
            reader.GetFieldValue<DateTimeOffset>(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }
}
