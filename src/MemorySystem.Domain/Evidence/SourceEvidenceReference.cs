using MemorySystem.Domain.Sensitivity;
using MemorySystem.Domain.Trust;

namespace MemorySystem.Domain.Evidence;

public sealed record SourceEvidenceReference
{
    public SourceEvidenceReference(
        Guid sourceEventId,
        MemoryTrustLevel trustLevel,
        MemorySensitivity sensitivity)
    {
        if (sourceEventId == Guid.Empty)
        {
            throw new ArgumentException("sourceEventId must not be empty.", nameof(sourceEventId));
        }

        ArgumentNullException.ThrowIfNull(trustLevel);
        ArgumentNullException.ThrowIfNull(sensitivity);

        SourceEventId = sourceEventId;
        TrustLevel = trustLevel;
        Sensitivity = sensitivity;
    }

    public Guid SourceEventId { get; }

    public MemoryTrustLevel TrustLevel { get; }

    public MemorySensitivity Sensitivity { get; }

    public static SourceEvidenceReference Create(
        Guid sourceEventId,
        string trustLevel,
        string sensitivity)
    {
        if (!TryCreate(sourceEventId, trustLevel, sensitivity, out var reference, out var error))
        {
            throw new ArgumentException(error);
        }

        return reference!;
    }

    public static bool TryCreate(
        Guid sourceEventId,
        string? trustLevel,
        string? sensitivity,
        out SourceEvidenceReference? reference,
        out string? error)
    {
        reference = null;
        error = null;

        if (!MemoryTrustLevel.TryNormalize(trustLevel, out var normalizedTrustLevel, out error))
        {
            return false;
        }

        if (!MemorySensitivity.TryNormalize(sensitivity, out var normalizedSensitivity, out error))
        {
            return false;
        }

        reference = new SourceEvidenceReference(
            sourceEventId,
            normalizedTrustLevel!,
            normalizedSensitivity!);
        return true;
    }
}
