namespace MemorySystem.Application.VaultExports;

public interface IObsidianExportCandidateStore
{
    Task<IReadOnlyList<ObsidianExportCandidate>> ListCandidatesAsync(
        ObsidianExportCandidateQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ObsidianStaleExportCandidate>> ListStaleCandidatesAsync(
        ObsidianExportCandidateQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ObsidianArchiveExportCandidate>> ListArchiveCandidatesAsync(
        ObsidianExportCandidateQuery query,
        CancellationToken cancellationToken = default);

    Task RecordExportedAsync(
        IReadOnlyList<ObsidianExportDocument> documents,
        CancellationToken cancellationToken = default);

    Task RecordStaleAsync(
        IReadOnlyList<ObsidianStaleExportDocument> documents,
        CancellationToken cancellationToken = default);

    Task RecordArchiveAsync(
        IReadOnlyList<ObsidianArchiveExportDocument> documents,
        CancellationToken cancellationToken = default);
}
