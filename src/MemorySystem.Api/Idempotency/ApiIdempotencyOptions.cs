namespace MemorySystem.Api.Idempotency;

public sealed class ApiIdempotencyOptions
{
    public const string SectionName = "ApiIdempotency";

    public string HeaderName { get; set; } = "Idempotency-Key";

    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    public int MaxKeyLength { get; set; } = 200;

    public static bool IsValid(ApiIdempotencyOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.HeaderName)
            && options.RetentionPeriod > TimeSpan.Zero
            && options.MaxKeyLength is > 0 and <= 500;
    }
}
