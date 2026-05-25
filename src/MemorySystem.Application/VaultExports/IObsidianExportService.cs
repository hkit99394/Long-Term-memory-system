namespace MemorySystem.Application.VaultExports;

public interface IObsidianExportService
{
    Task<ObsidianExportBundle> ExportAsync(
        ObsidianExportQuery query,
        CancellationToken cancellationToken = default);

    Task<ObsidianArchiveExportBundle> ExportArchiveAsync(
        ObsidianExportQuery query,
        CancellationToken cancellationToken = default);
}
