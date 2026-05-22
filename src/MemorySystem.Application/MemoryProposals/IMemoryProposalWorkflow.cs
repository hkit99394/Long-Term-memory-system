namespace MemorySystem.Application.MemoryProposals;

public interface IMemoryProposalWorkflow
{
    Task<MemoryProposalWorkflowResult> DecideAsync(
        MemoryProposalWorkflowRequest request,
        CancellationToken cancellationToken = default);
}
