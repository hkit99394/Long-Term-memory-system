namespace MemorySystem.Application.Events;

public interface ISourceEventLinkBuilder
{
    string Build(Guid sourceEventId);
}
