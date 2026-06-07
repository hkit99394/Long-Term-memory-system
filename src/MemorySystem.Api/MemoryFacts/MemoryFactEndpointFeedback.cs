using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using MemorySystem.Api.Http;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Operations;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.MemoryFacts;

public static partial class MemoryFactEndpointExtensions
{
    private static async Task<FeedbackCommandResult> TryCreateFeedbackCommandAsync(
        Guid principalId,
        MemoryContextFeedbackRequest request,
        IMemoryContextPacketObservationStore contextPacketObservations,
        CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim();

        if (request.PacketId == Guid.Empty)
        {
            return FeedbackCommandResult.Failure("packetId is invalid.");
        }

        if (request.ItemId == Guid.Empty)
        {
            return FeedbackCommandResult.Failure("itemId is invalid.");
        }

        if (request.ItemId.HasValue && !request.PacketId.HasValue)
        {
            return FeedbackCommandResult.Failure("itemId requires packetId.");
        }

        if (string.IsNullOrWhiteSpace(query) && !request.PacketId.HasValue)
        {
            return FeedbackCommandResult.Failure("query or packetId is required.");
        }

        string? error;
        if (!MemoryRetrievalFeedbackTypes.TryNormalize(request.FeedbackType, out var feedbackType, out error)
            || !TryNormalizeOptionalTargetScope(request.TargetScopeType, request.TargetScopeId, out var targetScopeType, out var targetScopeId, out error)
            || !TryNormalizeOptionalRoleId(request.RoleId, out var roleId, out error)
            || !TryNormalizeOptionalFeedbackSource(request.SourceType, request.SourceId, request.ItemId, feedbackType, out var sourceType, out error))
        {
            return FeedbackCommandResult.Failure(error);
        }

        string queryHash;
        if (string.IsNullOrWhiteSpace(query))
        {
            var observation = await contextPacketObservations.FindAsync(
                request.PacketId!.Value,
                principalId,
                cancellationToken);

            if (observation is null)
            {
                return FeedbackCommandResult.Failure("packetId must identify a context packet previously returned to the caller.");
            }

            if (!MatchesRequestedTargetScope(targetScopeType, targetScopeId, observation))
            {
                return FeedbackCommandResult.Failure("targetScopeType and targetScopeId must match the recorded context packet.");
            }

            if (roleId is not null && !string.Equals(roleId, observation.RoleId, StringComparison.Ordinal))
            {
                return FeedbackCommandResult.Failure("roleId must match the recorded context packet.");
            }

            queryHash = observation.QueryHash;
            targetScopeType ??= observation.TargetScopeType;
            targetScopeId ??= observation.TargetScopeId;
            roleId ??= observation.RoleId;
        }
        else
        {
            queryHash = ComputeSha256(query);
        }

        var command = new MemoryRetrievalFeedbackCommand(
            principalId,
            "context_packet",
            queryHash,
            request.PacketId,
            request.ItemId,
            targetScopeType,
            targetScopeId,
            roleId,
            sourceType,
            request.SourceId,
            feedbackType);

        return FeedbackCommandResult.Success(command);
    }

    private static bool MatchesRequestedTargetScope(
        string? targetScopeType,
        string? targetScopeId,
        MemoryContextPacketObservation observation)
    {
        if (targetScopeType is null && targetScopeId is null)
        {
            return true;
        }

        return string.Equals(targetScopeType, observation.TargetScopeType, StringComparison.Ordinal)
            && string.Equals(targetScopeId, observation.TargetScopeId, StringComparison.Ordinal);
    }

    private sealed record FeedbackCommandResult(
        bool Succeeded,
        MemoryRetrievalFeedbackCommand? Command,
        string? Error)
    {
        public static FeedbackCommandResult Success(MemoryRetrievalFeedbackCommand command)
        {
            return new FeedbackCommandResult(true, command, null);
        }

        public static FeedbackCommandResult Failure(string? error)
        {
            return new FeedbackCommandResult(false, null, error ?? "Feedback request is invalid.");
        }
    }
}
