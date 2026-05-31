using MemorySystem.Domain.Evidence;

namespace MemorySystem.Application.MemoryContext;

public sealed record MemoryContextSourceEvent(
    Guid Id,
    string? Link)
{
    public SourceEvidenceLink? ToDomain()
    {
        return string.IsNullOrWhiteSpace(Link)
            ? null
            : new SourceEvidenceLink(Id, Link);
    }

    public static MemoryContextSourceEvent FromDomain(SourceEvidenceLink sourceEvidenceLink)
    {
        ArgumentNullException.ThrowIfNull(sourceEvidenceLink);

        return new MemoryContextSourceEvent(
            sourceEvidenceLink.SourceEventId,
            sourceEvidenceLink.Link);
    }
}
