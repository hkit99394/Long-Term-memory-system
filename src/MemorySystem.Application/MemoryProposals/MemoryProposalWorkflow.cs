using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryProposals;

public sealed class MemoryProposalWorkflow(
    IMemoryProposalBroker broker,
    IMemoryProposalWriteStore writeStore,
    ISourceEventReferenceStore sourceEvents,
    IMemoryScopeResolver scopeResolver,
    IMemoryAccessAuthorizer accessAuthorizer) : IMemoryProposalWorkflow
{
    private static readonly IReadOnlySet<string> MemoryTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "preference",
        "decision",
        "fact",
        "role_principle",
        "project_role_lens",
        "agent_private",
        "session_instruction"
    };

    private static readonly IReadOnlySet<string> Visibilities = new HashSet<string>(StringComparer.Ordinal)
    {
        "private",
        "role_shared",
        "project_shared",
        "org_shared",
        "system"
    };

    public async Task<MemoryProposalWorkflowResult> DecideAsync(
        MemoryProposalWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestHash);

        if (!TryMap(request, sourceEventExists: false, out var proposal, out var error))
        {
            return MemoryProposalWorkflowResult.Invalid(error!);
        }

        var scopeResult = await scopeResolver.ResolveProposalScopeAsync(
            new MemoryProposalScopeRequest(
                request.AuthenticatedPrincipalId,
                request.ScopeType,
                request.ScopeId,
                request.Namespace),
            cancellationToken);

        if (!scopeResult.Succeeded)
        {
            return MemoryProposalWorkflowResult.Invalid(scopeResult.Error!);
        }

        proposal = proposal with
        {
            ScopeType = scopeResult.Resolution!.ScopeType,
            ScopeId = scopeResult.Resolution.ScopeId
        };

        if (!MemoryProposalDecisionRules.IsSessionOnly(proposal))
        {
            var accessDecision = await accessAuthorizer.AuthorizeAsync(
                new MemoryAccessRequest(
                    request.AuthenticatedPrincipalId,
                    MemoryAccessPermissions.Write,
                    scopeResult.Resolution,
                    proposal.Namespace),
                cancellationToken);

            if (!accessDecision.Allowed)
            {
                return MemoryProposalWorkflowResult.Forbidden(accessDecision.Reason!);
            }
        }

        var sourceEvent = await FindSourceEventAsync(
            request.AuthenticatedPrincipalId,
            proposal,
            cancellationToken);
        proposal = proposal with
        {
            SourceEventExists = sourceEvent is not null,
            TrustLevel = sourceEvent?.TrustLevel ?? proposal.TrustLevel
        };

        var decision = broker.Decide(proposal);

        if (decision.Decision != MemoryProposalDecisions.Stored)
        {
            return MemoryProposalWorkflowResult.Decided(decision);
        }

        decision = await writeStore.StoreAsync(
            request.AuthenticatedPrincipalId,
            proposal,
            request.IdempotencyRecordId,
            request.RequestHash,
            cancellationToken);

        return MemoryProposalWorkflowResult.Stored(decision);
    }

    private static bool TryMap(
        MemoryProposalWorkflowRequest request,
        bool sourceEventExists,
        out MemoryProposalCommand command,
        out string? error)
    {
        command = null!;
        error = null;

        var memoryType = Normalize(request.MemoryType);
        var scopeType = Normalize(request.ScopeType);
        var scopeId = request.ScopeId?.Trim() ?? string.Empty;
        var namespaceValue = request.Namespace?.Trim() ?? string.Empty;
        var visibility = Normalize(request.Visibility, "private");
        var trustLevel = Normalize(request.TrustLevel, "user_scoped");
        var sensitivity = Normalize(request.Sensitivity, "none");

        if (!MemoryTypes.Contains(memoryType))
        {
            error = "memoryType is required and must be supported.";
            return false;
        }

        if (!MemoryScopePolicy.ScopeTypes.Contains(scopeType))
        {
            error = "scopeType is required and must be a supported scope.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            error = "scopeId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(namespaceValue) || !namespaceValue.StartsWith("/", StringComparison.Ordinal))
        {
            error = "namespace is required and must start with '/'.";
            return false;
        }

        if (!Visibilities.Contains(visibility))
        {
            error = "visibility is not supported.";
            return false;
        }

        if (!MemoryScopePolicy.TrustLevels.Contains(trustLevel))
        {
            error = "trustLevel is not supported.";
            return false;
        }

        if (!MemoryTrustPolicy.IsExternallyAccepted(trustLevel))
        {
            error = "trustLevel requires a trusted internal source.";
            return false;
        }

        if (!MemoryScopePolicy.Sensitivities.Contains(sensitivity))
        {
            error = "sensitivity is not supported.";
            return false;
        }

        if (request.Confidence is < 0 or > 1)
        {
            error = "confidence must be between 0 and 1 when provided.";
            return false;
        }

        command = new MemoryProposalCommand(
            request.SourceEventId,
            sourceEventExists,
            memoryType,
            scopeType,
            scopeId,
            namespaceValue,
            visibility,
            request.Subject?.Trim() ?? string.Empty,
            request.Predicate?.Trim() ?? string.Empty,
            request.Object?.Trim() ?? string.Empty,
            request.Confidence,
            trustLevel,
            sensitivity);
        return true;
    }

    private async Task<SourceEventReference?> FindSourceEventAsync(
        Guid principalId,
        MemoryProposalCommand proposal,
        CancellationToken cancellationToken)
    {
        return proposal.SourceEventId.HasValue
            ? await sourceEvents.FindForPrincipalScopeAsync(
                proposal.SourceEventId.Value,
                principalId,
                proposal.ScopeType,
                proposal.ScopeId,
                cancellationToken)
            : null;
    }

    private static string Normalize(string? value, string defaultValue = "")
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();
    }
}
