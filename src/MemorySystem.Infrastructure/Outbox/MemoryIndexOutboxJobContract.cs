using System.Text.Json;

namespace MemorySystem.Infrastructure.Outbox;

public static class MemoryIndexOutboxJobContract
{
    public const string JobType = "memory.index";
    public const string AggregateType = "memory_fact";
    public const string RoleMemoryLensAggregateType = "role_memory_lens";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateIdempotencyKey(Guid memoryFactId)
    {
        return $"{JobType}:{memoryFactId:N}";
    }

    public static string CreateIdempotencyKey(string aggregateType, Guid aggregateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

        return string.Equals(aggregateType, AggregateType, StringComparison.Ordinal)
            ? CreateIdempotencyKey(aggregateId)
            : $"{JobType}:{aggregateType}:{aggregateId:N}";
    }

    public static string SerializePayload(Guid memoryFactId, Guid chunkId, Guid sourceEventId)
    {
        return SerializePayload(AggregateType, memoryFactId, chunkId, sourceEventId);
    }

    public static string SerializePayload(
        string aggregateType,
        Guid aggregateId,
        Guid chunkId,
        Guid sourceEventId)
    {
        return JsonSerializer.Serialize(
            new MemoryIndexOutboxPayload(aggregateType, aggregateId, chunkId, sourceEventId),
            JsonOptions);
    }

    public static MemoryIndexOutboxPayload ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException("Memory index payload is required.");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<MemoryIndexOutboxPayload>(payloadJson, JsonOptions);

            if (payload is null
                || string.IsNullOrWhiteSpace(payload.AggregateType)
                || !IsSupportedAggregateType(payload.AggregateType)
                || payload.AggregateId == Guid.Empty
                || payload.ChunkId == Guid.Empty
                || payload.SourceEventId == Guid.Empty)
            {
                return ParseLegacyPayload(payloadJson);
            }

            return payload;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Memory index payload is not valid JSON.", exception);
        }
    }

    public static bool IsSupportedAggregateType(string aggregateType)
    {
        return aggregateType is AggregateType or RoleMemoryLensAggregateType;
    }

    private static MemoryIndexOutboxPayload ParseLegacyPayload(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<LegacyMemoryIndexOutboxPayload>(payloadJson, JsonOptions);

        if (payload is null
            || payload.MemoryFactId == Guid.Empty
            || payload.ChunkId == Guid.Empty
            || payload.SourceEventId == Guid.Empty)
        {
            throw new InvalidOperationException("Memory index payload is invalid.");
        }

        return new MemoryIndexOutboxPayload(
            AggregateType,
            payload.MemoryFactId,
            payload.ChunkId,
            payload.SourceEventId);
    }
}

public sealed record MemoryIndexOutboxPayload(
    string AggregateType,
    Guid AggregateId,
    Guid ChunkId,
    Guid SourceEventId);

internal sealed record LegacyMemoryIndexOutboxPayload(
    Guid MemoryFactId,
    Guid ChunkId,
    Guid SourceEventId);
