using Microsoft.AspNetCore.Authentication;

namespace MemorySystem.Api.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "Authentication:ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";

    public Dictionary<string, ApiKeyCredentialOptions> Keys { get; set; } = new(StringComparer.Ordinal);

    public static bool AllowsMissingKeys(string environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasConfiguredKeys(ApiKeyAuthenticationOptions options)
    {
        var keys = options.Keys;

        return keys is { Count: > 0 } && keys.All(IsConfigured);
    }

    public static bool HasOnlyConfiguredKeys(ApiKeyAuthenticationOptions options)
    {
        var keys = options.Keys;

        return keys is not null && keys.All(IsConfigured);
    }

    public static bool HasValidPrincipalIds(ApiKeyAuthenticationOptions options)
    {
        var keys = options.Keys;

        return keys is not null
            && keys.Values.All(credential =>
                string.IsNullOrWhiteSpace(credential?.PrincipalId)
                || Guid.TryParse(credential.PrincipalId, out _));
    }

    public static bool HasDistinctKeyValues(ApiKeyAuthenticationOptions options)
    {
        var keys = options.Keys;

        if (keys is null)
        {
            return false;
        }

        var configuredSecrets = keys.Values
            .Select(credential => credential?.Key)
            .Where(secret => !string.IsNullOrWhiteSpace(secret))
            .ToArray();

        return configuredSecrets.Length == configuredSecrets.Distinct(StringComparer.Ordinal).Count();
    }

    private static bool IsConfigured(KeyValuePair<string, ApiKeyCredentialOptions> entry)
    {
        return !string.IsNullOrWhiteSpace(entry.Key)
            && entry.Value is not null
            && !string.IsNullOrWhiteSpace(entry.Value.Key)
            && !string.IsNullOrWhiteSpace(entry.Value.PrincipalId);
    }
}

public sealed class ApiKeyCredentialOptions
{
    public string? Key { get; set; }

    public string? PrincipalId { get; set; }

    public string? DisplayName { get; set; }
}
