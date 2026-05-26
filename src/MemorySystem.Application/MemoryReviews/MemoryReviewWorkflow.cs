using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryProposals;

namespace MemorySystem.Application.MemoryReviews;

public sealed class MemoryReviewWorkflow(
    IMemoryReviewActionStore actionStore,
    ISourceEventReferenceStore sourceEvents,
    IMemoryAccessAuthorizer accessAuthorizer) : IMemoryReviewWorkflow
{
    public async Task<MemoryReviewWorkflowResult> CompleteAsync(
        MemoryReviewActionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PrincipalId == Guid.Empty)
        {
            return MemoryReviewWorkflowResult.Invalid("Authenticated principal id is required.");
        }

        if (command.IdempotencyRecordId == Guid.Empty)
        {
            return MemoryReviewWorkflowResult.Invalid("Idempotency record id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RequestHash))
        {
            return MemoryReviewWorkflowResult.Invalid("Idempotency request hash is required.");
        }

        if (command.ReviewId == Guid.Empty)
        {
            return MemoryReviewWorkflowResult.Invalid("Review id is required.");
        }

        if (command.SourceEventId == Guid.Empty)
        {
            return MemoryReviewWorkflowResult.Invalid("sourceEventId is required.");
        }

        var action = command.Action.Trim().ToLowerInvariant();

        if (!MemoryReviewActions.All.Contains(action))
        {
            return MemoryReviewWorkflowResult.Invalid("Review action is not supported.");
        }

        if (ActionRequiresReplacementContent(action)
            && (string.IsNullOrWhiteSpace(command.Subject)
                || string.IsNullOrWhiteSpace(command.Predicate)
                || string.IsNullOrWhiteSpace(command.Object)))
        {
            return MemoryReviewWorkflowResult.Invalid("subject, predicate, and object are required for this review action.");
        }

        var review = await actionStore.FindPendingAsync(command.ReviewId, cancellationToken);

        if (review is null)
        {
            return MemoryReviewWorkflowResult.NotFound("The pending review does not exist or has already been completed.");
        }

        var accessDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                command.PrincipalId,
                MemoryAccessPermissions.Review,
                review.MemoryFact.ToScopeResolution(),
                review.MemoryFact.Namespace),
            cancellationToken);

        if (!accessDecision.Allowed)
        {
            return MemoryReviewWorkflowResult.Forbidden(accessDecision.Reason!);
        }

        var sourceEvent = await sourceEvents.FindForPrincipalScopeAsync(
            command.SourceEventId,
            command.PrincipalId,
            review.MemoryFact.ScopeType,
            review.MemoryFact.ScopeId,
            cancellationToken);

        if (sourceEvent is null)
        {
            return MemoryReviewWorkflowResult.Invalid("sourceEventId must reference accessible review evidence for the memory scope.");
        }

        var result = await actionStore.ApplyAsync(
            new MemoryReviewActionStoreCommand(
                review,
                action,
                command.PrincipalId,
                command.SourceEventId,
                command.IdempotencyRecordId,
                command.RequestHash,
                string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
                command.Subject?.Trim(),
                command.Predicate?.Trim(),
                command.Object?.Trim()),
            cancellationToken);

        return MemoryReviewWorkflowResult.Completed(
            action,
            result.Review,
            result.ReplacementMemoryFactId,
            idempotencyAlreadyCompleted: true);
    }

    private static bool ActionRequiresReplacementContent(string action)
    {
        return action is MemoryReviewActions.Edit or MemoryReviewActions.Supersede;
    }
}
