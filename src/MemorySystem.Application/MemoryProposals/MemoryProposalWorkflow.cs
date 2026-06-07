using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Roles;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.MemoryTypes;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Sensitivity;
using MemorySystem.Domain.Trust;

namespace MemorySystem.Application.MemoryProposals;

public sealed class MemoryProposalWorkflow(
    IMemoryProposalBroker broker,
    IMemoryProposalWriteStore writeStore,
    ISourceEventReferenceStore sourceEvents,
    IMemoryFactRepository memoryFacts,
    IMemoryScopeResolver scopeResolver,
    IMemoryAccessAuthorizer accessAuthorizer,
    IProjectRoleDefinitionStore projectRoleDefinitions) : IMemoryProposalWorkflow
{
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
                ScopeResolutionNamespaceFor(proposal)),
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

        if (proposal.CandidateKind != MemoryCandidateClassifications.RoleLens
            && IsReservedRoleLensNamespace(proposal.Namespace))
        {
            return MemoryProposalWorkflowResult.Invalid(
                "Role-lens namespaces are reserved for role-lens proposals.");
        }

        if (!TryValidateRoleLensStructure(
            scopeResult.Resolution,
            proposal,
            out var namespaceError))
        {
            return MemoryProposalWorkflowResult.Invalid(namespaceError!);
        }

        var roleDefinitionFailure = await ValidateRoleLensRoleDefinitionAsync(
            scopeResult.Resolution,
            proposal,
            cancellationToken);
        if (roleDefinitionFailure is not null)
        {
            return roleDefinitionFailure;
        }

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
        var sourceEvidence = sourceEvent?.ToDomain();
        proposal = proposal with
        {
            SourceEventExists = sourceEvent is not null,
            TrustLevel = sourceEvidence?.TrustLevel.Value ?? proposal.TrustLevel,
            Sensitivity = sourceEvidence is null
                ? proposal.Sensitivity
                : MoreRestrictiveSensitivity(proposal.Sensitivity, sourceEvidence.Sensitivity.Value)
        };

        var decision = broker.Decide(proposal);
        proposal = ApplyDecisionConfidence(proposal, decision);

        if (decision.Decision is MemoryProposalDecisions.Rejected or MemoryProposalDecisions.SessionOnly)
        {
            return MemoryProposalWorkflowResult.Decided(decision);
        }

        if (proposal.CandidateKind == MemoryCandidateClassifications.RoleLens)
        {
            var roleLensBaseFactFailure = await ValidateRoleLensBaseMemoryFactAsync(
                request.AuthenticatedPrincipalId,
                scopeResult.Resolution!,
                proposal,
                cancellationToken);

            if (roleLensBaseFactFailure is not null)
            {
                return roleLensBaseFactFailure;
            }
        }
        else if (decision.Decision == MemoryProposalDecisions.Stored)
        {
            var activeMemoryCandidates = await FindActiveMemoryCandidatesAsync(
                scopeResult.Resolution!,
                proposal,
                cancellationToken);

            var conflictingActiveMemory = FindConflictingActiveMemory(activeMemoryCandidates, proposal);

            if (conflictingActiveMemory is not null)
            {
                return ReviewRequired(
                    proposal,
                    "A conflicting active memory already exists and should be reviewed before storing another version.");
            }

            var similarActiveMemory = FindSimilarActiveMemory(activeMemoryCandidates, proposal);

            if (similarActiveMemory is not null)
            {
                return ReviewRequired(
                    proposal,
                    "A similar active memory already exists and should be reviewed before storing another version.");
            }
        }

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

        return decision.Decision == MemoryProposalDecisions.Stored
            ? MemoryProposalWorkflowResult.Stored(decision)
            : MemoryProposalWorkflowResult.CompletedDecision(decision);
    }

    private static MemoryProposalWorkflowResult ReviewRequired(
        MemoryProposalCommand proposal,
        string reason)
    {
        return MemoryProposalWorkflowResult.Decided(new MemoryProposalDecision(
            MemoryProposalDecisions.ReviewRequired,
            reason,
            MemoryId: null,
            proposal.SourceEventId,
            proposal.CandidateKind,
            proposal.Confidence));
    }

    private static MemoryProposalCommand ApplyDecisionConfidence(
        MemoryProposalCommand proposal,
        MemoryProposalDecision decision)
    {
        return decision.Confidence.HasValue
            ? proposal with { Confidence = decision.Confidence.Value }
            : proposal;
    }

    private static bool TryMap(
        MemoryProposalWorkflowRequest request,
        bool sourceEventExists,
        out MemoryProposalCommand command,
        out string? error)
    {
        command = null!;
        error = null;

        var scopeType = Normalize(request.ScopeType);
        var scopeId = request.ScopeId?.Trim() ?? string.Empty;
        var namespaceValue = request.Namespace?.Trim() ?? string.Empty;
        var visibility = Normalize(request.Visibility, "private");
        var trustLevel = Normalize(request.TrustLevel, MemoryTrustLevel.UserScoped);
        var sensitivity = Normalize(request.Sensitivity, MemorySensitivity.None);

        if (!MemoryType.TryNormalizeProposalType(request.MemoryType, out var memoryType, out _))
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
            memoryType!.Value,
            scopeType,
            scopeId,
            namespaceValue,
            visibility,
            request.Subject?.Trim() ?? string.Empty,
            request.Predicate?.Trim() ?? string.Empty,
            request.Object?.Trim() ?? string.Empty,
            request.Confidence,
            trustLevel,
            sensitivity,
            Normalize(request.RoleId),
            request.BaseMemoryFactId);
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

    private static bool TryValidateRoleLensStructure(
        MemoryScopeResolution scope,
        MemoryProposalCommand proposal,
        out string? error)
    {
        error = null;

        if (proposal.CandidateKind != MemoryCandidateClassifications.RoleLens)
        {
            return true;
        }

        if (scope.ScopeType is not ("global" or "org" or "project"))
        {
            error = "Role-lens proposals require global, organization, or project scope.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(proposal.RoleId)
            || !MemoryRoleId.TryNormalizeIdentifier(proposal.RoleId, out _, out _))
        {
            error = "Role-lens proposals require a supported roleId.";
            return false;
        }

        if (!proposal.BaseMemoryFactId.HasValue || proposal.BaseMemoryFactId.Value == Guid.Empty)
        {
            error = "baseMemoryFactId is required for role-lens proposals.";
            return false;
        }

        var canonicalNamespace = BuildRoleLensCanonicalNamespace(scope, proposal.RoleId);

        if (!IsNamespaceAtOrBelow(proposal.Namespace, canonicalNamespace))
        {
            error = $"Role-lens namespace must match roleId '{proposal.RoleId}' and start with '{canonicalNamespace}'.";
            return false;
        }

        return true;
    }

    private static string ScopeResolutionNamespaceFor(MemoryProposalCommand proposal)
    {
        if (proposal.CandidateKind == MemoryCandidateClassifications.RoleLens
            && proposal.ScopeType == "global"
            && !string.IsNullOrWhiteSpace(proposal.RoleId)
            && MemoryRoleId.IsDefaultTemplate(proposal.RoleId)
            && IsNamespaceAtOrBelow(proposal.Namespace, $"/role/{proposal.RoleId}/shared"))
        {
            return "/global/role-lens";
        }

        return proposal.Namespace;
    }

    private static string BuildRoleLensCanonicalNamespace(MemoryScopeResolution scope, string roleId)
    {
        return scope.ScopeType switch
        {
            "global" => $"/role/{roleId}/shared",
            "org" => $"/org/{RequireGuid(scope.OrgId, "Organization role-lens namespace requires organization scope.")}/role/{roleId}/lens",
            "project" => $"/project/{RequireGuid(scope.ProjectId, "Project role-lens namespace requires project scope.")}/role/{roleId}/lens",
            _ => throw new InvalidOperationException($"Unsupported role-lens scope type '{scope.ScopeType}'.")
        };
    }

    private static bool IsNamespaceAtOrBelow(string namespaceValue, string expectedNamespace)
    {
        return string.Equals(namespaceValue, expectedNamespace, StringComparison.Ordinal)
            || namespaceValue.StartsWith(expectedNamespace + "/", StringComparison.Ordinal);
    }

    private static bool IsReservedRoleLensNamespace(string namespaceValue)
    {
        if (!MemoryNamespaceParser.TryParse(namespaceValue, out var memoryNamespace, out _))
        {
            return false;
        }

        var segments = memoryNamespace.Segments;

        return memoryNamespace.ScopeType switch
        {
            "role" => segments.Count >= 3
                && memoryNamespace.RoleId is not null
                && string.Equals(segments[2], "shared", StringComparison.Ordinal),
            "org" or "project" => segments.Count >= 5
                && string.Equals(segments[2], "role", StringComparison.Ordinal)
                && memoryNamespace.RoleId is not null
                && string.Equals(segments[4], "lens", StringComparison.Ordinal),
            _ => false
        };
    }

    private async Task<MemoryProposalWorkflowResult?> ValidateRoleLensRoleDefinitionAsync(
        MemoryScopeResolution scope,
        MemoryProposalCommand proposal,
        CancellationToken cancellationToken)
    {
        if (proposal.CandidateKind != MemoryCandidateClassifications.RoleLens)
        {
            return null;
        }

        if (scope.ScopeType == "project")
        {
            if (!scope.ProjectId.HasValue)
            {
                return MemoryProposalWorkflowResult.Invalid("Project role-lens proposals require project scope.");
            }

            if (await projectRoleDefinitions.IsActiveProjectRoleAsync(
                    scope.ProjectId.Value,
                    proposal.RoleId!,
                    cancellationToken))
            {
                return null;
            }

            return MemoryProposalWorkflowResult.Invalid(
                "Role-lens proposals require a default role template or active project role definition.");
        }

        return MemoryRoleId.IsDefaultTemplate(proposal.RoleId)
            ? null
            : MemoryProposalWorkflowResult.Invalid(
                "Role-lens proposals outside project scope require a default role template.");
    }

    private static Guid RequireGuid(Guid? value, string message)
    {
        return value ?? throw new InvalidOperationException(message);
    }

    private async Task<IReadOnlyList<MemoryFactRecord>> FindActiveMemoryCandidatesAsync(
        MemoryScopeResolution scope,
        MemoryProposalCommand proposal,
        CancellationToken cancellationToken)
    {
        return await memoryFacts.FindActiveBySubjectPredicateAsync(
            new MemoryFactSubjectPredicateQuery(
                scope,
                proposal.MemoryType,
                proposal.Subject,
                proposal.Predicate),
            cancellationToken);
    }

    private async Task<MemoryProposalWorkflowResult?> ValidateRoleLensBaseMemoryFactAsync(
        Guid authenticatedPrincipalId,
        MemoryScopeResolution scope,
        MemoryProposalCommand proposal,
        CancellationToken cancellationToken)
    {
        if (!proposal.BaseMemoryFactId.HasValue || proposal.BaseMemoryFactId.Value == Guid.Empty)
        {
            return MemoryProposalWorkflowResult.Invalid("baseMemoryFactId is required for role-lens proposals.");
        }

        var baseMemoryFact = await memoryFacts.FindAsync(
            proposal.BaseMemoryFactId.Value,
            cancellationToken);

        if (baseMemoryFact is null)
        {
            return MemoryProposalWorkflowResult.Invalid("baseMemoryFactId must reference an existing memory fact.");
        }

        var accessDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                authenticatedPrincipalId,
                MemoryAccessPermissions.Read,
                baseMemoryFact.ToScopeResolution(),
                baseMemoryFact.Namespace),
            cancellationToken);

        if (!accessDecision.Allowed)
        {
            return MemoryProposalWorkflowResult.Forbidden(accessDecision.Reason!);
        }

        if (!string.Equals(baseMemoryFact.Status, MemoryFactStatuses.Active, StringComparison.Ordinal))
        {
            return MemoryProposalWorkflowResult.Invalid("baseMemoryFactId must reference an active memory fact.");
        }

        return IsValidRoleLensBaseMemoryFactScope(scope, baseMemoryFact)
            ? null
            : MemoryProposalWorkflowResult.Invalid(BuildInvalidRoleLensBaseScopeMessage(scope.ScopeType));
    }

    private static bool IsValidRoleLensBaseMemoryFactScope(
        MemoryScopeResolution scope,
        MemoryFactRecord baseMemoryFact)
    {
        return scope.ScopeType switch
        {
            "global" => baseMemoryFact.ScopeType == "global",
            "org" => baseMemoryFact.ScopeType == "org"
                && baseMemoryFact.OrgId == scope.OrgId,
            "project" => (baseMemoryFact.ScopeType == "project" && baseMemoryFact.ProjectId == scope.ProjectId)
                || (baseMemoryFact.ScopeType == "org" && baseMemoryFact.OrgId == scope.OrgId),
            _ => false
        };
    }

    private static string BuildInvalidRoleLensBaseScopeMessage(string scopeType)
    {
        return scopeType switch
        {
            "global" => "Global role-lens proposals must reference global memory facts.",
            "org" => "Organization role-lens proposals must reference memory facts from the same organization.",
            "project" => "Project role-lens proposals must reference memory facts from the target project or its organization.",
            _ => $"Role-lens proposals do not support scope type '{scopeType}'."
        };
    }

    private static string MoreRestrictiveSensitivity(string requestSensitivity, string sourceEventSensitivity)
    {
        return SensitivityRank(sourceEventSensitivity) > SensitivityRank(requestSensitivity)
            ? sourceEventSensitivity
            : requestSensitivity;
    }

    private static int SensitivityRank(string sensitivity)
    {
        return MemorySensitivity.TryNormalize(sensitivity, out var normalizedSensitivity, out _)
            ? normalizedSensitivity!.Rank
            : 0;
    }

    private static MemoryFactRecord? FindConflictingActiveMemory(
        IReadOnlyList<MemoryFactRecord> memoryFactsInScope,
        MemoryProposalCommand proposal)
    {
        return memoryFactsInScope.FirstOrDefault(memoryFact =>
            SameText(memoryFact.Subject, proposal.Subject)
            && SameText(memoryFact.Predicate, proposal.Predicate)
            && !SameText(memoryFact.Object, proposal.Object)
            && MemoryProposalContradictionRules.AreContradictory(memoryFact.Object, proposal.Object));
    }

    private static MemoryFactRecord? FindSimilarActiveMemory(
        IReadOnlyList<MemoryFactRecord> memoryFactsInScope,
        MemoryProposalCommand proposal)
    {
        return memoryFactsInScope.FirstOrDefault(memoryFact =>
            SameText(memoryFact.Subject, proposal.Subject)
            && SameText(memoryFact.Predicate, proposal.Predicate)
            && !SameText(memoryFact.Object, proposal.Object));
    }

    private static bool SameText(string left, string right)
    {
        return string.Equals(
            NormalizeComparableText(left),
            NormalizeComparableText(right),
            StringComparison.Ordinal);
    }

    private static string NormalizeComparableText(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string Normalize(string? value, string defaultValue = "")
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();
    }
}
