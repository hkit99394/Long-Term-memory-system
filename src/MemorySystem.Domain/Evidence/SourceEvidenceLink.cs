namespace MemorySystem.Domain.Evidence;

public sealed record SourceEvidenceLink
{
    public SourceEvidenceLink(Guid sourceEventId, string link)
    {
        if (sourceEventId == Guid.Empty)
        {
            throw new ArgumentException("sourceEventId must not be empty.", nameof(sourceEventId));
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            throw new ArgumentException("link is required.", nameof(link));
        }

        SourceEventId = sourceEventId;
        Link = link;
    }

    public Guid SourceEventId { get; }

    public string Link { get; }
}
