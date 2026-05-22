namespace MemorySystem.Application.MemoryFacts;

public sealed record MemoryFactReadResult(MemoryFactRecord? MemoryFact)
{
    public bool Found => MemoryFact is not null;

    public static MemoryFactReadResult FoundMemoryFact(MemoryFactRecord memoryFact)
    {
        ArgumentNullException.ThrowIfNull(memoryFact);

        return new MemoryFactReadResult(memoryFact);
    }

    public static MemoryFactReadResult NotFound()
    {
        return new MemoryFactReadResult((MemoryFactRecord?)null);
    }
}
