using ApplicationFeedbackTypes = MemorySystem.Application.MemoryEvaluations.MemoryRetrievalFeedbackTypes;
using ApplicationFactStatuses = MemorySystem.Application.MemoryFacts.MemoryFactStatuses;
using ApplicationMemoryScopeResolution = MemorySystem.Application.Scopes.MemoryScopeResolution;
using ApplicationNamespaceParser = MemorySystem.Application.Scopes.MemoryNamespaceParser;
using ApplicationRetentionClasses = MemorySystem.Application.Retention.MemoryRetentionClasses;
using ApplicationScopePolicy = MemorySystem.Application.Scopes.MemoryScopePolicy;
using ApplicationSourceEventReference = MemorySystem.Application.MemoryProposals.SourceEventReference;
using ApplicationTrustPolicy = MemorySystem.Application.Scopes.MemoryTrustPolicy;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Domain.Evidence;
using MemorySystem.Domain.Lifecycle;
using MemorySystem.Domain.MemoryTypes;
using MemorySystem.Domain.Namespaces;
using MemorySystem.Domain.Retention;
using MemorySystem.Domain.Retrieval;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;
using MemorySystem.Domain.Sensitivity;
using MemorySystem.Domain.Trust;

namespace MemorySystem.UnitTests;

public sealed class DomainValueObjectCompatibilityTests
{
    [Fact]
    public void Domain_scope_vocabulary_matches_application_policy()
    {
        Assert.Equal(ApplicationScopePolicy.ScopeTypes.Order(), MemoryScopeType.All.Order());
        Assert.Equal(ApplicationScopePolicy.RoleIds.Order(), MemoryRoleId.All.Order());
        Assert.Equal(ApplicationScopePolicy.TrustLevels.Order(), MemoryTrustLevel.All.Order());
        Assert.Equal(ApplicationScopePolicy.Sensitivities.Order(), MemorySensitivity.All.Order());
        Assert.Equal(ApplicationRetentionClasses.All.Order(), MemoryRetentionClass.All.Order());
    }

    [Theory]
    [InlineData(" PROJECT ", "33333333-3333-4333-8333-333333333333", "project", "33333333-3333-4333-8333-333333333333")]
    [InlineData("role", "CTO", "role", "cto")]
    [InlineData("global", "GLOBAL", "global", "global")]
    [InlineData("session", "session-1", "session", "session-1")]
    public void Domain_scope_normalization_matches_target_scope_policy(
        string scopeType,
        string scopeId,
        string expectedScopeType,
        string expectedScopeId)
    {
        var applicationResult = ApplicationScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out var applicationScopeType,
            out var applicationScopeId,
            out var applicationError);

        var domainResult = MemoryScope.TryNormalize(
            scopeType,
            scopeId,
            out var domainScope,
            out var domainError);

        Assert.True(applicationResult);
        Assert.True(domainResult);
        Assert.Null(applicationError);
        Assert.Null(domainError);
        Assert.Equal(expectedScopeType, applicationScopeType);
        Assert.Equal(expectedScopeId, applicationScopeId);
        Assert.Equal(applicationScopeType, domainScope!.ScopeType);
        Assert.Equal(applicationScopeId, domainScope.ScopeId);
    }

    [Theory]
    [InlineData("project", "not-a-guid", "valid GUID")]
    [InlineData("global", "not-global", "must be 'global'")]
    [InlineData("role", "intern", "supported role")]
    [InlineData("session", "global", "must not be 'global'")]
    public void Domain_scope_rejections_match_target_scope_policy(
        string scopeType,
        string scopeId,
        string expectedError)
    {
        var applicationResult = ApplicationScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out _,
            out _,
            out var applicationError);

        var domainResult = MemoryScope.TryNormalize(scopeType, scopeId, out _, out var domainError);

        Assert.False(applicationResult);
        Assert.False(domainResult);
        Assert.Contains(expectedError, applicationError, StringComparison.Ordinal);
        Assert.Contains(expectedError, domainError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CTO", "cto")]
    [InlineData(" Security_Professional ", "security_professional")]
    [InlineData(" cfo ", "cfo")]
    public void Domain_role_normalization_matches_application_policy(string roleId, string expectedRoleId)
    {
        var applicationResult = ApplicationScopePolicy.TryNormalizeRoleId(roleId, out var applicationRoleId, out var applicationError);
        var domainResult = MemoryRoleId.TryNormalize(roleId, out var domainRoleId, out var domainError);

        Assert.True(applicationResult);
        Assert.True(domainResult);
        Assert.Null(applicationError);
        Assert.Null(domainError);
        Assert.Equal(expectedRoleId, applicationRoleId);
        Assert.Equal(applicationRoleId, domainRoleId!.Value);
    }

    [Fact]
    public void Domain_project_role_namespace_accepts_custom_project_role_identifier()
    {
        var namespaceValue = "/project/33333333-3333-4333-8333-333333333333/role/Implementation_Lead/lens";

        var applicationResult = ApplicationNamespaceParser.TryParse(
            namespaceValue,
            out var applicationNamespace,
            out var applicationError);
        var domainResult = MemoryNamespaceParser.TryParse(
            namespaceValue,
            out var domainNamespace,
            out var domainError);

        Assert.True(applicationResult);
        Assert.True(domainResult);
        Assert.Null(applicationError);
        Assert.Null(domainError);
        Assert.Equal("implementation_lead", applicationNamespace.RoleId);
        Assert.Equal("implementation_lead", domainNamespace!.RoleId?.Value);
    }

    [Fact]
    public void Domain_org_and_shared_role_namespaces_keep_default_template_roles()
    {
        Assert.False(MemoryNamespaceParser.TryParse(
            "/org/22222222-2222-4222-8222-222222222222/role/implementation_lead/lens",
            out _,
            out var orgError));
        Assert.Contains("supported", orgError, StringComparison.Ordinal);

        Assert.False(MemoryNamespaceParser.TryParse(
            "/role/implementation_lead/shared",
            out _,
            out var sharedError));
        Assert.Contains("supported", sharedError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/global/instructions", "global", "global", null)]
    [InlineData("/org/22222222-2222-4222-8222-222222222222/policies", "org", "22222222-2222-4222-8222-222222222222", null)]
    [InlineData("/user/11111111-1111-4111-8111-111111111111/preferences", "user", "11111111-1111-4111-8111-111111111111", null)]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/decisions", "project", "33333333-3333-4333-8333-333333333333", null)]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/role/CTO/lens", "project", "33333333-3333-4333-8333-333333333333", "cto")]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/role/Tester_QA/lens", "project", "33333333-3333-4333-8333-333333333333", "tester_qa")]
    [InlineData("/org/22222222-2222-4222-8222-222222222222/role/CTO/lens", "org", "22222222-2222-4222-8222-222222222222", "cto")]
    [InlineData("/role/CTO/shared", "role", "cto", "cto")]
    [InlineData("/agent/44444444-4444-4444-8444-444444444444/private", "agent", "44444444-4444-4444-8444-444444444444", null)]
    [InlineData("/session/session%1/working_memory", "session", "session%1", null)]
    public void Domain_namespace_parser_matches_application_parser(
        string namespaceValue,
        string expectedScopeType,
        string expectedScopeId,
        string? expectedRoleId)
    {
        var applicationResult = ApplicationNamespaceParser.TryParse(namespaceValue, out var applicationNamespace, out var applicationError);
        var domainResult = MemoryNamespaceParser.TryParse(namespaceValue, out var domainNamespace, out var domainError);

        Assert.True(applicationResult);
        Assert.True(domainResult);
        Assert.Null(applicationError);
        Assert.Null(domainError);
        Assert.Equal(expectedScopeType, domainNamespace!.ScopeType);
        Assert.Equal(expectedScopeId, domainNamespace.ScopeId);
        Assert.Equal(expectedRoleId, domainNamespace.RoleId?.Value);
        Assert.Equal(applicationNamespace.ScopeType, domainNamespace.ScopeType);
        Assert.Equal(applicationNamespace.ScopeId, domainNamespace.ScopeId);
        Assert.Equal(applicationNamespace.RoleId, domainNamespace.RoleId?.Value);
        Assert.Equal(applicationNamespace.Segments, domainNamespace.Segments);
    }

    [Theory]
    [InlineData("", "must start")]
    [InlineData("global/instructions", "must start")]
    [InlineData("/global", "category")]
    [InlineData("/global/", "empty path")]
    [InlineData("/project/not-a-guid/decisions", "valid GUID")]
    [InlineData("/org/22222222-2222-4222-8222-222222222222/role/cto", "category")]
    [InlineData("/project/33333333-3333-4333-8333-333333333333/role/1intern/lens", "not supported")]
    [InlineData("/role/intern/shared", "not supported")]
    [InlineData("/session/global/working_memory", "must not be 'global'")]
    [InlineData("/session/session-1/../working_memory", "relative path")]
    public void Domain_namespace_parser_rejections_match_application_parser(string namespaceValue, string expectedError)
    {
        var applicationResult = ApplicationNamespaceParser.TryParse(namespaceValue, out _, out var applicationError);
        var domainResult = MemoryNamespaceParser.TryParse(namespaceValue, out _, out var domainError);

        Assert.False(applicationResult);
        Assert.False(domainResult);
        Assert.Contains(expectedError, applicationError, StringComparison.Ordinal);
        Assert.Contains(expectedError, domainError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("global", "global", "/global/")]
    [InlineData("project", "33333333-3333-4333-8333-333333333333", "/project/33333333-3333-4333-8333-333333333333/")]
    [InlineData("session", "session-1", "/session/session-1/")]
    public void Domain_scope_prefix_builder_matches_application_facade(
        string scopeType,
        string scopeId,
        string expectedPrefix)
    {
        Assert.Equal(expectedPrefix, ApplicationNamespaceParser.BuildScopePrefix(scopeType, scopeId));
        Assert.Equal(expectedPrefix, MemoryNamespaceParser.BuildScopePrefix(scopeType, scopeId));
    }

    [Fact]
    public void Domain_lifecycle_vocabulary_matches_application_statuses()
    {
        Assert.Equal(ApplicationFactStatuses.All.Order(), MemoryLifecycleStatus.All.Order());

        foreach (var status in ApplicationFactStatuses.All)
        {
            Assert.True(MemoryLifecycleStatus.TryParse(status, out var lifecycleStatus, out var error));
            Assert.Null(error);
            Assert.Equal(status, lifecycleStatus!.Value);
            Assert.Equal(
                ApplicationFactStatuses.IsNormalRetrievalStatus(status),
                lifecycleStatus.IsNormalRetrievalStatus);
        }

        Assert.False(MemoryLifecycleStatus.IsSupported("ACTIVE"));
    }

    [Fact]
    public void Domain_memory_type_vocabulary_defines_ip06_canonical_durable_types()
    {
        var canonicalTypes = new[]
        {
            "goal",
            "target",
            "fact",
            "decision",
            "rationale",
            "risk",
            "assumption",
            "constraint",
            "requirement",
            "release_evidence",
            "role_lens"
        };

        Assert.Equal(canonicalTypes.Order(), MemoryType.CanonicalDurable.Order());

        foreach (var memoryType in canonicalTypes)
        {
            Assert.True(MemoryType.TryNormalizeProposalType(memoryType.ToUpperInvariant(), out var proposalType, out var proposalError));
            Assert.True(MemoryType.TryNormalizeQueryableType($" {memoryType} ", out var queryableType, out var queryableError));
            Assert.Null(proposalError);
            Assert.Null(queryableError);
            Assert.Equal(memoryType, proposalType!.Value);
            Assert.Equal(memoryType, queryableType!.Value);
            Assert.True(proposalType.IsCanonicalDurable);
        }
    }

    [Fact]
    public void Domain_memory_type_vocabulary_keeps_legacy_query_aliases_separate_from_canonical_set()
    {
        Assert.True(MemoryType.TryNormalizeQueryableType("summary", out var summary, out _));
        Assert.Equal("summary", summary!.Value);
        Assert.False(summary.IsCanonicalDurable);

        Assert.False(MemoryType.TryNormalizeProposalType("summary", out _, out var proposalError));
        Assert.Contains("supported", proposalError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("system_trusted", false)]
    [InlineData("human_approved", false)]
    [InlineData("user_scoped", true)]
    [InlineData("agent_private", true)]
    [InlineData("tool_output", true)]
    [InlineData("retrieved_untrusted", true)]
    [InlineData("web_content", true)]
    public void Domain_trust_policy_matches_application_policy(string value, bool externallyAccepted)
    {
        Assert.True(MemoryTrustLevel.TryNormalize(value, out var trustLevel, out var error));
        Assert.Null(error);
        Assert.Equal(externallyAccepted, trustLevel!.IsExternallyAccepted);
        Assert.Equal(ApplicationTrustPolicy.IsExternallyAccepted(value), trustLevel.IsExternallyAccepted);
    }

    [Theory]
    [InlineData("ephemeral")]
    [InlineData("standard")]
    [InlineData("audit")]
    [InlineData("legal_hold")]
    [InlineData("erasure_requested")]
    public void Domain_retention_class_round_trips_supported_schema_values(string value)
    {
        Assert.True(MemoryRetentionClass.TryNormalize(value, out var retentionClass, out var error));
        Assert.Null(error);
        Assert.True(ApplicationRetentionClasses.IsSupported(value));
        Assert.Equal(value, retentionClass!.Value);
        Assert.Equal(value == "erasure_requested", retentionClass.HidesRawPayloadFromNormalReads);
    }

    [Theory]
    [InlineData("none", 0, false)]
    [InlineData("personal", 1, false)]
    [InlineData("secret", 2, true)]
    [InlineData("regulated", 3, true)]
    public void Domain_sensitivity_round_trips_policy_values(
        string value,
        int expectedRank,
        bool blocksContext)
    {
        Assert.True(MemorySensitivity.TryNormalize(value, out var sensitivity, out var error));
        Assert.Null(error);
        Assert.Equal(value, sensitivity!.Value);
        Assert.Equal(expectedRank, sensitivity.Rank);
        Assert.Equal(blocksContext, sensitivity.BlocksDirectContextInjection);
    }

    [Theory]
    [InlineData("useful", "useful", true)]
    [InlineData("stale", "stale", true)]
    [InlineData("wrong", "wrong", true)]
    [InlineData("sensitive", "sensitive", true)]
    [InlineData("over-broad", "over_broad", true)]
    [InlineData("over_broad", "over_broad", true)]
    [InlineData("missing", "missing", false)]
    [InlineData("noisy", "noisy", true)]
    public void Domain_feedback_type_matches_application_feedback_policy(
        string input,
        string expected,
        bool requiresSource)
    {
        var applicationResult = ApplicationFeedbackTypes.TryNormalize(input, out var applicationFeedbackType, out var applicationError);
        var domainResult = MemoryRetrievalFeedbackType.TryNormalize(input, out var domainFeedbackType, out var domainError);

        Assert.True(applicationResult);
        Assert.True(domainResult);
        Assert.Null(applicationError);
        Assert.Null(domainError);
        Assert.Equal(expected, applicationFeedbackType);
        Assert.Equal(applicationFeedbackType, domainFeedbackType!.Value);
        Assert.Equal(requiresSource, ApplicationFeedbackTypes.RequiresSource(applicationFeedbackType));
        Assert.Equal(requiresSource, domainFeedbackType.RequiresSource);
    }

    [Fact]
    public void Source_evidence_reference_requires_non_empty_source_event()
    {
        Assert.True(MemoryTrustLevel.TryNormalize("user_scoped", out var trustLevel, out _));
        Assert.True(MemorySensitivity.TryNormalize("none", out var sensitivity, out _));

        var sourceEventId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var reference = new SourceEvidenceReference(sourceEventId, trustLevel!, sensitivity!);
        var link = new SourceEvidenceLink(sourceEventId, $"/api/events/{sourceEventId}");

        Assert.Equal(sourceEventId, reference.SourceEventId);
        Assert.Equal("user_scoped", reference.TrustLevel.Value);
        Assert.Equal("none", reference.Sensitivity.Value);
        Assert.Equal($"/api/events/{sourceEventId}", link.Link);
        Assert.Throws<ArgumentException>(() => new SourceEvidenceReference(Guid.Empty, trustLevel!, sensitivity!));
        Assert.Throws<ArgumentException>(() => new SourceEvidenceLink(Guid.Empty, "/api/events/empty"));
    }

    [Fact]
    public void Application_source_evidence_facades_round_trip_domain_values()
    {
        var sourceEventId = Guid.Parse("99999999-9999-4999-8999-999999999999");
        var sourceLink = $"/api/events/{sourceEventId}";

        var proposalReference = ApplicationSourceEventReference.FromValues(
            sourceEventId,
            " USER_SCOPED ",
            " NONE ");
        var proposalEvidence = proposalReference.ToDomain();

        Assert.Equal(sourceEventId, proposalReference.Id);
        Assert.Equal("user_scoped", proposalReference.TrustLevel);
        Assert.Equal("none", proposalReference.Sensitivity);
        Assert.Equal(sourceEventId, proposalEvidence.SourceEventId);
        Assert.Equal("user_scoped", proposalEvidence.TrustLevel.Value);
        Assert.Equal("none", proposalEvidence.Sensitivity.Value);

        var domainLink = new SourceEvidenceLink(sourceEventId, sourceLink);
        var contextSourceEvent = MemoryContextSourceEvent.FromDomain(domainLink);
        Assert.Equal(sourceEventId, contextSourceEvent.Id);
        Assert.Equal(sourceLink, contextSourceEvent.Link);
        Assert.Equal(domainLink, contextSourceEvent.ToDomain());

        var eventRecord = new EventRecord(
            sourceEventId,
            PrincipalId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            ConversationId: null,
            AgentPrincipalId: null,
            RoleId: null,
            EventType: "user_message",
            ContentJson: "{}",
            ContentHash: null,
            ExternalPayloadUri: null,
            RetentionClass: "standard",
            Sensitivity: " NONE ",
            TrustLevel: " USER_SCOPED ",
            CreatedAt: DateTimeOffset.UnixEpoch,
            Scope: new ApplicationMemoryScopeResolution(
                "project",
                "33333333-3333-4333-8333-333333333333",
                ProjectId: Guid.Parse("33333333-3333-4333-8333-333333333333")));

        var eventEvidence = eventRecord.ToSourceEvidenceReference();
        Assert.Equal(sourceEventId, eventEvidence.SourceEventId);
        Assert.Equal("user_scoped", eventEvidence.TrustLevel.Value);
        Assert.Equal("none", eventEvidence.Sensitivity.Value);
    }
}
