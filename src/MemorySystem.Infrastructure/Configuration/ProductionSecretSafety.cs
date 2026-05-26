namespace MemorySystem.Infrastructure.Configuration;

public static class ProductionSecretSafety
{
    public const int DefaultMinimumSecretLength = 16;

    private static readonly HashSet<string> KnownUnsafeSecretValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "api-key",
        "changeme",
        "change-me",
        "dev-key",
        "development-key",
        "dummy-key",
        "environment-test-key",
        "example-key",
        "local-key",
        "memory_system_dev_password",
        "password",
        "placeholder",
        "sample-key",
        "secret",
        "second-test-api-key",
        "test-api-key",
        "test-key",
        "test-openai-key"
    };

    public static bool IsProductionSafeSecretValue(
        string? value,
        int minimumLength = DefaultMinimumSecretLength)
    {
        if (minimumLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength), "Minimum secret length must be positive.");
        }

        var trimmedValue = value?.Trim();

        return !string.IsNullOrWhiteSpace(trimmedValue)
            && trimmedValue.Length >= minimumLength
            && !IsKnownUnsafeSecretValue(trimmedValue);
    }

    public static bool IsKnownUnsafeSecretValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && KnownUnsafeSecretValues.Contains(value.Trim());
    }
}
