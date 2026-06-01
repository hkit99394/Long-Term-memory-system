using MemorySystem.Application.Authentication;
using Npgsql;

namespace MemorySystem.Infrastructure.Authentication;

public sealed class PostgresPrincipalResolver(
    NpgsqlDataSource dataSource,
    IIdentityBindingStore identityBindingStore) : IPrincipalResolver
{
    public async Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
        ApiKeyPrincipalResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                principal_type
            FROM principals
            WHERE id = @principal_id
                AND status = 'active'
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("principal_id", request.PrincipalId);

        string principalType;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            principalType = reader.GetString(1);
        }

        var credentialId = request.ApiKeyId;
        if (string.Equals(principalType, "service", StringComparison.Ordinal))
        {
            var serviceCredentialId = await MarkActiveServiceCredentialUsedAsync(
                connection,
                request,
                cancellationToken);
            if (!serviceCredentialId.HasValue)
            {
                return null;
            }

            credentialId = serviceCredentialId.Value.ToString("D");
        }

        return new AuthenticatedPrincipal(
            request.PrincipalId,
            principalType,
            request.DisplayName,
            AuthenticationMethods.ApiKey,
            credentialId);
    }

    public async Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
        IdentityBindingLookup lookup,
        string authMethod = AuthenticationMethods.Oidc,
        CancellationToken cancellationToken = default)
    {
        var binding = await identityBindingStore.FindActiveAsync(lookup, cancellationToken);
        if (binding is null)
        {
            return null;
        }

        return new AuthenticatedPrincipal(
            binding.PrincipalId,
            binding.PrincipalType,
            binding.DisplayName,
            authMethod,
            binding.BindingId.ToString("D"),
            binding.Issuer,
            binding.Subject);
    }

    private static async Task<Guid?> MarkActiveServiceCredentialUsedAsync(
        NpgsqlConnection connection,
        ApiKeyPrincipalResolutionRequest request,
        CancellationToken cancellationToken)
    {
        var configuredCredentialId = string.IsNullOrWhiteSpace(request.ServiceCredentialId)
            ? request.ApiKeyId
            : request.ServiceCredentialId;

        if (!Guid.TryParse(configuredCredentialId, out var credentialId))
        {
            return null;
        }

        await using var command = new NpgsqlCommand(
            """
            UPDATE service_account_credentials AS credential
            SET last_used_at = now()
            FROM service_accounts AS account
            WHERE credential.id = @credential_id
                AND credential.service_principal_id = @principal_id
                AND credential.service_principal_id = account.principal_id
                AND credential.auth_method = @auth_method
                AND credential.status = 'active'
                AND (credential.expires_at IS NULL OR credential.expires_at > now())
                AND account.status = 'active'
                AND account.allowed_auth_method = @auth_method
                AND (account.expires_at IS NULL OR account.expires_at > now())
            RETURNING credential.id;
            """,
            connection);

        command.Parameters.AddWithValue("credential_id", credentialId);
        command.Parameters.AddWithValue("principal_id", request.PrincipalId);
        command.Parameters.AddWithValue("auth_method", AuthenticationMethods.ApiKey);

        return await command.ExecuteScalarAsync(cancellationToken) is Guid activeCredentialId
            ? activeCredentialId
            : null;
    }
}
