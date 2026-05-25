namespace MemorySystem.Application.Events;

public sealed record EventReadResult(EventRecord? Event)
{
    public bool Found => Event is not null;

    public static EventReadResult FoundEvent(EventRecord eventRecord)
    {
        ArgumentNullException.ThrowIfNull(eventRecord);

        return new EventReadResult(eventRecord);
    }

    public static EventReadResult NotFound()
    {
        return new EventReadResult((EventRecord?)null);
    }
}
