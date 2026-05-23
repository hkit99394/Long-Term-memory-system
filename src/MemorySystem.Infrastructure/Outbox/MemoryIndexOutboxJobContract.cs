using System.Text.Json;

namespace MemorySystem.Infrastructure.Outbox;

public static class MemoryIndexOutboxJobContract
{
    public const string JobType = "memory.index";
    public const string AggregateType = "memory_fact";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateIdempotencyKey(Guid memoryFactId)
    {
        return $"{JobType}:{memoryFactId:N}";
    }

    public static string SerializePayload(Guid memoryFactId, Guid chunkId, Guid sourceEventId)
    {
        return JsonSerializer.Serialize(
            new MemoryIndexOutboxPayload(memoryFactId, chunkId, sourceEventId),
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
                || payload.MemoryFactId == Guid.Empty
                || payload.ChunkId == Guid.Empty
                || payload.SourceEventId == Guid.Empty)
            {
                throw new InvalidOperationException("Memory index payload is invalid.");
            }

            return payload;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Memory index payload is not valid JSON.", exception);
        }
    }
}

public sealed record MemoryIndexOutboxPayload(
    Guid MemoryFactId,
    Guid ChunkId,
    Guid SourceEventId);
