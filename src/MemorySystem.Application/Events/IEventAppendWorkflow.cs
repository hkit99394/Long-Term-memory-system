namespace MemorySystem.Application.Events;

public interface IEventAppendWorkflow
{
    Task<EventAppendWorkflowResult> AppendAsync(
        EventAppendWorkflowRequest request,
        CancellationToken cancellationToken = default);
}
