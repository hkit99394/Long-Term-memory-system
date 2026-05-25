namespace MemorySystem.Application.MemoryContext;

public interface IContextPacketBuilder
{
    Task<MemoryContextPacket> BuildAsync(
        MemoryContextPacketQuery query,
        CancellationToken cancellationToken = default);
}
