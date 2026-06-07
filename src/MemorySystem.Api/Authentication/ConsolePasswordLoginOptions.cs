using MemorySystem.Infrastructure.Configuration;

namespace MemorySystem.Api.Authentication;

public sealed class ConsolePasswordLoginOptions
{
    public const string SectionName = "Authentication:ConsolePassword";

    public bool Enabled { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? ApiKeyId { get; set; }

    public static bool HasRequiredConfiguration(ConsolePasswordLoginOptions options)
    {
        return !options.Enabled
            || (!string.IsNullOrWhiteSpace(options.Username)
                && !string.IsNullOrWhiteSpace(options.Password)
                && !string.IsNullOrWhiteSpace(options.ApiKeyId));
    }

    public static bool AllowsUnsafePassword(string environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasProductionSafePassword(ConsolePasswordLoginOptions options)
    {
        return !options.Enabled
            || ProductionSecretSafety.IsProductionSafeSecretValue(options.Password);
    }
}
