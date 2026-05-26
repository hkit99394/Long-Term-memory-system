namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryReviewWorkflowResult(
    bool Succeeded,
    string? Error,
    int FailureStatusCode,
    string? Action,
    MemoryReviewRecord? Review,
    Guid? ReplacementMemoryFactId,
    bool IdempotencyAlreadyCompleted = false)
{
    public static MemoryReviewWorkflowResult Completed(
        string action,
        MemoryReviewRecord review,
        Guid? replacementMemoryFactId = null,
        bool idempotencyAlreadyCompleted = false)
    {
        return new MemoryReviewWorkflowResult(
            true,
            Error: null,
            FailureStatusCode: 0,
            action,
            review,
            replacementMemoryFactId,
            idempotencyAlreadyCompleted);
    }

    public static MemoryReviewWorkflowResult Invalid(string error)
    {
        return Failure(400, error);
    }

    public static MemoryReviewWorkflowResult Forbidden(string error)
    {
        return Failure(403, error);
    }

    public static MemoryReviewWorkflowResult NotFound(string error)
    {
        return Failure(404, error);
    }

    private static MemoryReviewWorkflowResult Failure(int statusCode, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new MemoryReviewWorkflowResult(
            false,
            error,
            statusCode,
            Action: null,
            Review: null,
            ReplacementMemoryFactId: null);
    }
}
