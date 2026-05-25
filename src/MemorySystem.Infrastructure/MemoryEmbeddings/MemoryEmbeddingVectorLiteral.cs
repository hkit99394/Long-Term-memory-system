using System.Globalization;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

internal static class MemoryEmbeddingVectorLiteral
{
    public static string Format(IReadOnlyList<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var value in values)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException("Embedding values must be finite.", nameof(values));
            }
        }

        return "[" + string.Join(
            ",",
            values.Select(value => value.ToString("G9", CultureInfo.InvariantCulture))) + "]";
    }
}
