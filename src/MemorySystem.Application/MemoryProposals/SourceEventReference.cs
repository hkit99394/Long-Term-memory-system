using MemorySystem.Domain.Evidence;

namespace MemorySystem.Application.MemoryProposals;

public sealed record SourceEventReference(
    Guid Id,
    string TrustLevel,
    string Sensitivity)
{
    public SourceEvidenceReference ToDomain()
    {
        return SourceEvidenceReference.Create(Id, TrustLevel, Sensitivity);
    }

    public static SourceEventReference FromDomain(SourceEvidenceReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new SourceEventReference(
            reference.SourceEventId,
            reference.TrustLevel.Value,
            reference.Sensitivity.Value);
    }

    public static SourceEventReference FromValues(
        Guid id,
        string trustLevel,
        string sensitivity)
    {
        return FromDomain(SourceEvidenceReference.Create(id, trustLevel, sensitivity));
    }
}
