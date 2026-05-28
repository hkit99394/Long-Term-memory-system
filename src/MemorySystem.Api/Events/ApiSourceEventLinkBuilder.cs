using MemorySystem.Application.Events;

namespace MemorySystem.Api.Events;

internal sealed class ApiSourceEventLinkBuilder : ISourceEventLinkBuilder
{
    public string Build(Guid sourceEventId)
    {
        return $"/api/events/{sourceEventId}";
    }
}
