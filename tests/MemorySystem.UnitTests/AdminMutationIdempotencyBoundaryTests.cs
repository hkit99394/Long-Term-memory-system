namespace MemorySystem.UnitTests;

public sealed class AdminMutationIdempotencyBoundaryTests
{
    [Fact]
    public void Governance_and_registration_mutations_complete_idempotency_inside_the_write_boundary()
    {
        var root = FindRepositoryRoot();
        var governanceEndpoint = Read(root, "src", "MemorySystem.Api", "Admin", "AdminGovernanceEndpointExtensions.cs");
        var governanceContract = Read(root, "src", "MemorySystem.Application", "Admin", "IAdminGovernanceStore.cs");
        var governanceSharedStore = Read(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminGovernanceStore.Shared.cs");
        var governanceLegalHoldStore = Read(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminGovernanceStore.LegalHolds.cs");
        var governanceErasureStore = Read(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminGovernanceStore.Erasure.cs");
        var registrationEndpoint = Read(root, "src", "MemorySystem.Api", "Admin", "AdminProjectRegistrationEndpointExtensions.cs");
        var registrationStore = Read(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminProjectRegistrationStore.cs");

        Assert.Contains("Guid IdempotencyRecordId", governanceContract, StringComparison.Ordinal);
        Assert.Contains("string RequestHash", governanceContract, StringComparison.Ordinal);

        AssertContainsAll(
            governanceEndpoint,
            "idempotency.RecordId",
            "idempotency.RequestHash",
            "IdempotencyAlreadyCompleted: true");
        AssertContainsAll(
            registrationEndpoint,
            "idempotency.RecordId",
            "idempotency.RequestHash",
            "IdempotencyAlreadyCompleted: true");

        AssertContainsAll(
            governanceSharedStore,
            "PostgresApiIdempotencyCompleter.CompleteAsync",
            "connection",
            "transaction",
            "JsonContentType");

        AssertFirstCompletionBeforeFirstCommit(governanceLegalHoldStore, "await CompleteIdempotencyAsync(");
        AssertLastCompletionBeforeLastCommit(governanceLegalHoldStore, "await CompleteLegalHoldReleaseIdempotencyAsync(");
        AssertLastCompletionBeforeLastCommit(governanceErasureStore, "await CompleteIdempotencyAsync(");
        AssertLastCompletionBeforeLastCommit(registrationStore, "await PostgresApiIdempotencyCompleter.CompleteAsync");
    }

    private static void AssertFirstCompletionBeforeFirstCommit(string source, string completionCall)
    {
        var completionIndex = source.IndexOf(completionCall, StringComparison.Ordinal);
        var commitIndex = source.IndexOf("await transaction.CommitAsync", StringComparison.Ordinal);

        AssertCompletionPrecedesCommit(completionCall, completionIndex, commitIndex);
    }

    private static void AssertLastCompletionBeforeLastCommit(string source, string completionCall)
    {
        var completionIndex = source.LastIndexOf(completionCall, StringComparison.Ordinal);
        var commitIndex = source.LastIndexOf("await transaction.CommitAsync", StringComparison.Ordinal);

        AssertCompletionPrecedesCommit(completionCall, completionIndex, commitIndex);
    }

    private static void AssertCompletionPrecedesCommit(string completionCall, int completionIndex, int commitIndex)
    {
        Assert.True(completionIndex >= 0, $"Expected to find {completionCall}.");
        Assert.True(commitIndex >= 0, "Expected to find transaction commit.");
        Assert.True(completionIndex < commitIndex, $"{completionCall} must happen before transaction commit.");
    }

    private static void AssertContainsAll(string source, params string[] expectedFragments)
    {
        foreach (var fragment in expectedFragments)
        {
            Assert.Contains(fragment, source, StringComparison.Ordinal);
        }
    }

    private static string Read(string root, params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([root, .. pathParts]));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MemorySystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
