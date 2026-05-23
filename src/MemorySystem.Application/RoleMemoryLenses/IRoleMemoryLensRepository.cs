namespace MemorySystem.Application.RoleMemoryLenses;

public interface IRoleMemoryLensRepository
{
    Task<RoleMemoryLensRecord?> FindAsync(
        Guid roleMemoryLensId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleMemoryLensRecord>> FindByScopeAsync(
        RoleMemoryLensScopeQuery query,
        CancellationToken cancellationToken = default);

    Task<RoleMemoryLensRecord> StoreAsync(
        RoleMemoryLensWriteCommand command,
        CancellationToken cancellationToken = default);
}
