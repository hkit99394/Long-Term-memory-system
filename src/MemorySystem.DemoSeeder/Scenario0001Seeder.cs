using System.Security.Cryptography;
using System.Text;
using Npgsql;

internal static partial class Scenario0001Seeder
{
    public static async Task<Scenario0001SeedResult> SeedAsync(
        string connectionString,
        bool seedEmbeddings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await UpsertPrincipalAsync(connection, transaction, cancellationToken);
        await UpsertOrganizationsAndProjectsAsync(connection, transaction, cancellationToken);
        await UpsertMembershipsAsync(connection, transaction, cancellationToken);
        await UpsertRoleAssignmentsAsync(connection, transaction, cancellationToken);
        await UpsertAccessGrantsAsync(connection, transaction, cancellationToken);
        await UpsertEventsAsync(connection, transaction, cancellationToken);
        await UpsertMemoryFactsAsync(connection, transaction, cancellationToken);
        await UpsertRoleMemoryLensesAsync(connection, transaction, cancellationToken);
        await UpsertMemoryChunksAsync(connection, transaction, cancellationToken);
        await UpsertOutboxJobsAsync(connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var embeddingCount = seedEmbeddings
            ? await SeedEmbeddingsAsync(dataSource, cancellationToken)
            : 0;

        return new Scenario0001SeedResult(
            Scenario0001.EventIds.Length,
            Scenario0001.MemoryFactIds.Length,
            Scenario0001.RoleMemoryLensIds.Length,
            Scenario0001.ChunkIds.Length,
            embeddingCount);
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
