namespace MemorySystem.Application.Scopes;

public sealed class MemoryScopeResolver(IMemoryScopeReferenceStore referenceStore) : IMemoryScopeResolver
{
    public async Task<MemoryScopeResolveResult> ResolveEventScopeAsync(
        MemoryEventScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!MemoryEventScopePolicy.TryNormalizeEventScope(
            request.AuthenticatedPrincipalId,
            request.ScopeType,
            request.ScopeId,
            request.ScopeOrgId,
            request.ConversationId,
            request.AgentPrincipalId,
            request.RoleId,
            out var resolution,
            out var error))
        {
            return MemoryScopeResolveResult.Failure(error!);
        }

        return await ValidateResolvedScopeAsync(resolution, request.ScopeOrgId, cancellationToken);
    }

    public async Task<MemoryScopeResolveResult> ResolveProposalScopeAsync(
        MemoryProposalScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scopeType = Normalize(request.ScopeType);
        var scopeId = request.ScopeId?.Trim() ?? string.Empty;
        var namespaceValue = request.Namespace?.Trim() ?? string.Empty;

        if (!MemoryScopePolicy.TryNormalizeProposalScope(
            request.AuthenticatedPrincipalId,
            scopeType,
            scopeId,
            namespaceValue,
            out var normalizedScopeId,
            out var error))
        {
            return MemoryScopeResolveResult.Failure(error!);
        }

        var resolution = CreateProposalResolution(scopeType, normalizedScopeId);

        return await ValidateResolvedScopeAsync(resolution, requestedOrgId: null, cancellationToken);
    }

    private static MemoryScopeResolution CreateProposalResolution(string scopeType, string scopeId)
    {
        return scopeType switch
        {
            "global" => new MemoryScopeResolution("global", "global"),
            "org" => new MemoryScopeResolution("org", scopeId, OrgId: Guid.Parse(scopeId)),
            "project" => new MemoryScopeResolution("project", scopeId, ProjectId: Guid.Parse(scopeId)),
            "user" => new MemoryScopeResolution("user", scopeId, PrincipalId: Guid.Parse(scopeId)),
            "agent" => new MemoryScopeResolution("agent", scopeId, PrincipalId: Guid.Parse(scopeId), AgentPrincipalId: Guid.Parse(scopeId)),
            "role" => new MemoryScopeResolution("role", scopeId, RoleId: scopeId, ScopeRoleId: scopeId),
            "session" => new MemoryScopeResolution("session", scopeId),
            _ => throw new InvalidOperationException($"Unsupported memory scope type '{scopeType}'.")
        };
    }

    private async Task<MemoryScopeResolveResult> ValidateResolvedScopeAsync(
        MemoryScopeResolution resolution,
        Guid? requestedOrgId,
        CancellationToken cancellationToken)
    {
        switch (resolution.ScopeType)
        {
            case "global":
            case "role":
            case "session":
                return MemoryScopeResolveResult.Success(resolution);

            case "org":
                if (!resolution.OrgId.HasValue)
                {
                    return MemoryScopeResolveResult.Failure("Organization scope requires an organization id.");
                }

                if (!await referenceStore.OrganizationExistsAsync(resolution.OrgId.Value, cancellationToken))
                {
                    return MemoryScopeResolveResult.Failure($"Organization scope {resolution.OrgId.Value} does not exist.");
                }

                return MemoryScopeResolveResult.Success(resolution);

            case "project":
                if (!resolution.ProjectId.HasValue)
                {
                    return MemoryScopeResolveResult.Failure("Project scope requires a project id.");
                }

                var project = await referenceStore.FindProjectAsync(resolution.ProjectId.Value, cancellationToken);

                if (project is null)
                {
                    return MemoryScopeResolveResult.Failure($"Project scope {resolution.ProjectId.Value} does not exist.");
                }

                if (requestedOrgId.HasValue && requestedOrgId.Value != project.OrgId)
                {
                    return MemoryScopeResolveResult.Failure(
                        $"Project scope {project.ProjectId} does not belong to organization {requestedOrgId.Value}.");
                }

                return MemoryScopeResolveResult.Success(resolution with
                {
                    OrgId = project.OrgId,
                    ProjectId = project.ProjectId
                });

            case "user":
                if (!resolution.PrincipalId.HasValue)
                {
                    return MemoryScopeResolveResult.Failure("User scope requires a principal id.");
                }

                if (!await referenceStore.PrincipalExistsAsync(resolution.PrincipalId.Value, cancellationToken: cancellationToken))
                {
                    return MemoryScopeResolveResult.Failure($"User scope {resolution.PrincipalId.Value} does not reference an active principal.");
                }

                return MemoryScopeResolveResult.Success(resolution);

            case "agent":
                var agentPrincipalId = resolution.AgentPrincipalId ?? resolution.PrincipalId;

                if (!agentPrincipalId.HasValue)
                {
                    return MemoryScopeResolveResult.Failure("Agent scope requires an agent principal id.");
                }

                if (!await referenceStore.PrincipalExistsAsync(agentPrincipalId.Value, "agent", cancellationToken))
                {
                    return MemoryScopeResolveResult.Failure($"Agent scope {agentPrincipalId.Value} does not reference an active agent principal.");
                }

                return MemoryScopeResolveResult.Success(resolution with
                {
                    PrincipalId = agentPrincipalId,
                    AgentPrincipalId = agentPrincipalId
                });

            default:
                return MemoryScopeResolveResult.Failure($"Scope type '{resolution.ScopeType}' is not supported.");
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
