using Microsoft.AspNetCore.Authentication;

namespace MemorySystem.Api.Authentication;

public sealed class OidcAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "Authentication:Oidc";

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string JwksUri { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;

    public int ClockSkewSeconds { get; set; } = 300;

    public static bool HasRequiredConfiguration(OidcAuthenticationOptions options)
    {
        return !options.Enabled
            || (!string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && !string.IsNullOrWhiteSpace(options.JwksUri));
    }

    public static bool HasValidJwksUri(OidcAuthenticationOptions options)
    {
        return !options.Enabled
            || Uri.TryCreate(options.JwksUri, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    public static bool HasValidClockSkew(OidcAuthenticationOptions options)
    {
        return !options.Enabled || options.ClockSkewSeconds is >= 0 and <= 3600;
    }

    public static bool HasHttpsMetadataWhenRequired(
        OidcAuthenticationOptions options,
        string environmentName)
    {
        if (!options.Enabled || !options.RequireHttpsMetadata)
        {
            return true;
        }

        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(options.JwksUri, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}
