namespace MemorySystem.Application.AccessAuditing;

public interface IAccessAuditEventStore
{
    Task<AccessAuditEventRecord> RecordAsync(
        AccessAuditEventCommand command,
        CancellationToken cancellationToken = default);
}
