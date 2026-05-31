using MemorySystem.Domain.Evidence;

namespace MemorySystem.Application.Events;

public static class SourceEventLinkBuilderExtensions
{
    public static SourceEvidenceLink BuildEvidenceLink(
        this ISourceEventLinkBuilder sourceEventLinks,
        Guid sourceEventId)
    {
        ArgumentNullException.ThrowIfNull(sourceEventLinks);

        return new SourceEvidenceLink(sourceEventId, sourceEventLinks.Build(sourceEventId));
    }
}
