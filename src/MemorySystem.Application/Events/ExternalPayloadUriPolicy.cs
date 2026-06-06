namespace MemorySystem.Application.Events;

public sealed class ExternalPayloadUriPolicy
{
    public static ExternalPayloadUriPolicy Disabled { get; } =
        new([], [], AllowLocalFileUris: false);

    private readonly IReadOnlySet<string> allowedSchemes;
    private readonly IReadOnlyList<string> allowedPrefixes;

    public ExternalPayloadUriPolicy(
        IEnumerable<string> allowedSchemes,
        IEnumerable<string> allowedPrefixes,
        bool AllowLocalFileUris)
    {
        this.allowedSchemes = new HashSet<string>(
            NormalizeValues(allowedSchemes).Select(value => value.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        this.allowedPrefixes = NormalizeValues(allowedPrefixes).ToArray();
        this.AllowLocalFileUris = AllowLocalFileUris;
    }

    public bool AllowLocalFileUris { get; }

    public bool TryNormalize(
        string? value,
        out string? normalizedUri,
        out string? error)
    {
        normalizedUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        normalizedUri = value.Trim();

        if (!Uri.TryCreate(normalizedUri, UriKind.Absolute, out var uri))
        {
            error = "externalPayloadUri must be an absolute URI.";
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
            && !AllowLocalFileUris)
        {
            error = "externalPayloadUri file:// values are allowed only in local development or testing modes.";
            return false;
        }

        if (allowedPrefixes.Count > 0)
        {
            var candidateUri = normalizedUri;

            if (!allowedPrefixes.Any(prefix => candidateUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                error = "externalPayloadUri must match an allowed URI prefix.";
                return false;
            }

            if (allowedSchemes.Count > 0 && !allowedSchemes.Contains(uri.Scheme))
            {
                error = "externalPayloadUri uses a scheme that is not allowed.";
                return false;
            }

            return true;
        }

        if (allowedSchemes.Contains(uri.Scheme))
        {
            return true;
        }

        error = "externalPayloadUri must use an allowed URI scheme or prefix.";
        return false;
    }

    private static IEnumerable<string> NormalizeValues(IEnumerable<string>? values)
    {
        return values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim());
    }
}
