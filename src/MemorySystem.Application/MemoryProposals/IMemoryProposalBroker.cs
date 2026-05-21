namespace MemorySystem.Application.MemoryProposals;

public interface IMemoryProposalBroker
{
    MemoryProposalDecision Decide(MemoryProposalCommand proposal);
}
