namespace MemorySystem.Application.Events;

public sealed record EventAppendWorkflowResult(
    bool Succeeded,
    string? FailureTitle,
    string? FailureDetail,
    int FailureStatusCode,
    AppendEventResult? Result,
    string? ResourceType = null,
    Guid? ResourceId = null,
    bool IdempotencyAlreadyCompleted = false)
{
    public static EventAppendWorkflowResult InvalidRequest(string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return Failure(400, "Event request is invalid.", detail);
    }

    public static EventAppendWorkflowResult InvalidScope(string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return Failure(400, "Event scope is invalid.", detail);
    }

    public static EventAppendWorkflowResult Forbidden(string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return Failure(403, "Event scope is forbidden.", detail);
    }

    public static EventAppendWorkflowResult Stored(AppendEventResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new EventAppendWorkflowResult(
            true,
            FailureTitle: null,
            FailureDetail: null,
            FailureStatusCode: 0,
            result,
            "event",
            result.Id,
            IdempotencyAlreadyCompleted: true);
    }

    private static EventAppendWorkflowResult Failure(int statusCode, string title, string detail)
    {
        return new EventAppendWorkflowResult(
            false,
            title,
            detail,
            statusCode,
            Result: null);
    }
}
