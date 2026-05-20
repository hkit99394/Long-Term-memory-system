namespace MemorySystem.UnitTests;

public sealed class MigratorCliTests
{
    [Fact]
    public async Task RunAsync_returns_clean_error_for_unknown_option()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MigratorCli.RunAsync(["--unknown"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("Unknown option '--unknown'", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_returns_clean_error_for_missing_option_value()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MigratorCli.RunAsync(["--connection-string"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("requires a value", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_prints_help_without_resolving_connection_string()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MigratorCli.RunAsync(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Applies ordered SQL migrations", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--connection-string", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }
}
