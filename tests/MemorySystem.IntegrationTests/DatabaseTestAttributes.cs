namespace MemorySystem.IntegrationTests;

internal sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        Skip = DatabaseTestSkip.ShouldSkipWhenMissingConnectionString
            ? PostgresTestDatabase.MissingAdminConnectionStringSkipReason
            : null;
    }
}

internal sealed class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute()
    {
        Skip = DatabaseTestSkip.ShouldSkipWhenMissingConnectionString
            ? PostgresTestDatabase.MissingAdminConnectionStringSkipReason
            : null;
    }
}

internal static class DatabaseTestSkip
{
    public static bool ShouldSkipWhenMissingConnectionString =>
        !PostgresTestDatabase.HasAdminConnectionString
        && !IsContinuousIntegration()
        && !RequiresDatabaseTests();

    private static bool IsContinuousIntegration()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresDatabaseTests()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("MEMORYSYSTEM_REQUIRE_DATABASE_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }
}
