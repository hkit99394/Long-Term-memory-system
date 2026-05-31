namespace MemorySystem.Application.MemoryContext;

public interface IMemoryContextPacketObservationStore
{
    Task RecordAsync(
        MemoryContextPacketObservation observation,
        CancellationToken cancellationToken = default);

    Task<MemoryContextPacketObservation?> FindAsync(
        Guid packetId,
        Guid principalId,
        CancellationToken cancellationToken = default);
}

public sealed record MemoryContextPacketObservation(
    Guid PacketId,
    Guid PrincipalId,
    string QueryHash,
    string? TargetScopeType,
    string? TargetScopeId,
    string? RoleId,
    int ItemCount);
