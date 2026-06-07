using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Application.Roles;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Roles;

namespace MemorySystem.UnitTests;

public sealed class MemoryProposalWorkflowTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid IdempotencyRecordId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public async Task DecideAsync_returns_invalid_result_without_http_problem_details()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(memoryType: "unsupported"));

        Assert.False(result.IsValid);
        Assert.Equal("memoryType is required and must be supported.", result.InvalidReason);
        Assert.Null(result.Decision);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_missing_source_event_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: false);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest());

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Rejected, result.Decision?.Decision);
        Assert.Equal(SourceEventId, result.Decision?.SourceEventId);
        Assert.Null(result.ResourceType);
        Assert.False(result.IdempotencyAlreadyCompleted);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_returns_review_decision_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(confidence: 0.45m));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.ReviewRequired, result.Decision?.Decision);
        Assert.Equal(0.450m, result.Decision?.Confidence);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_stores_accepted_proposal_and_returns_resource_metadata()
    {
        var memoryId = Guid.Parse("88888888-8888-4888-8888-888888888888");
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore(memoryId);
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: " PREFERENCE ",
            scopeType: " USER ",
            visibility: null,
            trustLevel: null,
            sensitivity: null));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(memoryId, result.Decision?.MemoryId);
        Assert.Equal(MemoryCandidateClassifications.Preference, result.Decision?.CandidateKind);
        Assert.Equal("memory_fact", result.ResourceType);
        Assert.Equal(memoryId, result.ResourceId);
        Assert.True(result.IdempotencyAlreadyCompleted);

        Assert.Equal(1, writeStore.CallCount);
        Assert.Equal(PrincipalId, writeStore.ProposedByPrincipalId);
        Assert.Equal(IdempotencyRecordId, writeStore.IdempotencyRecordId);
        Assert.Equal("request-hash", writeStore.RequestHash);
        Assert.NotNull(writeStore.Proposal);
        Assert.True(writeStore.Proposal.SourceEventExists);
        Assert.Equal("preference", writeStore.Proposal.MemoryType);
        Assert.Equal("user", writeStore.Proposal.ScopeType);
        Assert.Equal(PrincipalId.ToString(), writeStore.Proposal.ScopeId);
        Assert.Equal("private", writeStore.Proposal.Visibility);
        Assert.Equal("user_scoped", writeStore.Proposal.TrustLevel);
        Assert.Equal("none", writeStore.Proposal.Sensitivity);
        Assert.Equal(0.900m, writeStore.Proposal.Confidence);
        Assert.Equal(0.900m, result.Decision?.Confidence);
    }

    [Theory]
    [InlineData("goal", "/goals")]
    [InlineData("target", "/targets")]
    [InlineData("fact", "/facts")]
    [InlineData("rationale", "/rationale")]
    [InlineData("risk", "/risks")]
    [InlineData("assumption", "/assumptions")]
    [InlineData("constraint", "/constraints")]
    [InlineData("requirement", "/requirements")]
    [InlineData("release_evidence", "/release-evidence")]
    public async Task DecideAsync_stores_canonical_project_memory_types(
        string memoryType,
        string namespaceSuffix)
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: memoryType,
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}{namespaceSuffix}",
            visibility: "project_shared",
            subject: "canonical memory type",
            predicate: "supports",
            objectValue: memoryType));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(MemoryCandidateClassifications.ProjectFact, result.Decision?.CandidateKind);
        Assert.Equal(memoryType, writeStore.Proposal.MemoryType);
        Assert.Equal(1, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_assigns_missing_confidence_before_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(confidence: null));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(0.850m, result.Decision?.Confidence);
        Assert.Equal(0.850m, writeStore.Proposal.Confidence);
    }

    [Fact]
    public async Task DecideAsync_routes_low_trust_source_event_to_review()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true, trustLevel: "web_content");
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(trustLevel: "tool_output"));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.ReviewRequired, result.Decision?.Decision);
        Assert.Equal(0.650m, result.Decision?.Confidence);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_derives_source_event_trust_level_before_scoring()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true, trustLevel: "tool_output");
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(trustLevel: "user_scoped"));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal("tool_output", writeStore.Proposal.TrustLevel);
        Assert.Equal(0.800m, result.Decision?.Confidence);
        Assert.Equal(0.800m, writeStore.Proposal.Confidence);
    }

    [Fact]
    public async Task DecideAsync_uses_more_restrictive_source_event_sensitivity()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true, sensitivity: "secret");
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(sensitivity: "none"));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.ReviewRequired, result.Decision?.Decision);
        Assert.Contains("sensitive content", result.Decision?.Reason, StringComparison.Ordinal);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_elevated_caller_supplied_trust_level()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var workflow = CreateWorkflow(sourceEvents, writeStore);

        var result = await workflow.DecideAsync(CreateRequest(trustLevel: "system_trusted"));

        Assert.False(result.IsValid);
        Assert.Equal("trustLevel requires a trusted internal source.", result.InvalidReason);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_returns_forbidden_result_without_writing_when_access_denied()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: false);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest());

        Assert.False(result.IsValid);
        Assert.Equal(403, result.FailureStatusCode);
        Assert.Contains("write access", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
        Assert.Equal(1, accessAuthorizer.CallCount);
    }

    [Fact]
    public async Task DecideAsync_allows_session_only_decision_without_access_preflight()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: false);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "session_instruction",
            scopeType: "session",
            namespaceValue: "/session/session-1/instructions"));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.SessionOnly, result.Decision?.Decision);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
        Assert.Equal(0, accessAuthorizer.CallCount);
    }

    [Fact]
    public async Task DecideAsync_keeps_one_off_instruction_session_only_without_access_preflight()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: false);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest(
            objectValue: "For this answer, keep it short."));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.SessionOnly, result.Decision?.Decision);
        Assert.Equal(MemoryCandidateClassifications.SessionOnlyInstruction, result.Decision?.CandidateKind);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
        Assert.Equal(0, accessAuthorizer.CallCount);
    }

    [Fact]
    public async Task DecideAsync_routes_conflicting_active_memory_to_review_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var memoryFacts = new FakeMemoryFactRepository(
            ProjectMemoryFact() with
            {
                ScopeType = "user",
                ScopeId = PrincipalId.ToString(),
                Namespace = $"/user/{PrincipalId}/preferences",
                UserPrincipalId = PrincipalId,
                ProjectId = null,
                OrgId = null,
                MemoryType = "preference",
                Visibility = "private",
                Subject = "technical planning format",
                Predicate = "prefers",
                Object = "enabled"
            });
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest(objectValue: "disabled"));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.ReviewRequired, result.Decision?.Decision);
        Assert.Equal(MemoryCandidateClassifications.Preference, result.Decision?.CandidateKind);
        Assert.Contains("conflicting active memory", result.Decision?.Reason, StringComparison.Ordinal);
        Assert.Equal(1, memoryFacts.SearchCallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_routes_similar_active_memory_to_review_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var memoryFacts = new FakeMemoryFactRepository(
            ProjectMemoryFact() with
            {
                ScopeType = "user",
                ScopeId = PrincipalId.ToString(),
                Namespace = $"/user/{PrincipalId}/preferences",
                UserPrincipalId = PrincipalId,
                ProjectId = null,
                OrgId = null,
                MemoryType = "preference",
                Visibility = "private",
                Subject = "technical planning format",
                Predicate = "prefers",
                Object = "verbose decision logs"
            });
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest());

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.ReviewRequired, result.Decision?.Decision);
        Assert.Equal(MemoryCandidateClassifications.Preference, result.Decision?.CandidateKind);
        Assert.Contains("similar active memory", result.Decision?.Reason, StringComparison.Ordinal);
        Assert.Equal(1, memoryFacts.SearchCallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_allows_exact_duplicate_to_reach_write_store()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var memoryFacts = new FakeMemoryFactRepository(
            ProjectMemoryFact() with
            {
                ScopeType = "user",
                ScopeId = PrincipalId.ToString(),
                Namespace = $"/user/{PrincipalId}/preferences",
                UserPrincipalId = PrincipalId,
                ProjectId = null,
                OrgId = null,
                MemoryType = "preference",
                Visibility = "private",
                Subject = "technical planning format",
                Predicate = "prefers",
                Object = "concise decision logs"
            });
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest());

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(1, memoryFacts.SearchCallCount);
        Assert.Equal(1, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_validates_role_lens_base_fact_before_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var baseMemoryFact = ProjectMemoryFact();
        var memoryFacts = new FakeMemoryFactRepository(baseMemoryFact);
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cto",
            baseMemoryFactId: baseMemoryFact.Id));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(MemoryCandidateClassifications.RoleLens, result.Decision?.CandidateKind);
        Assert.Equal("role_memory_lens", result.ResourceType);
        Assert.Equal(result.Decision?.MemoryId, result.ResourceId);
        Assert.Equal(1, memoryFacts.FindCallCount);
        Assert.Equal(0, memoryFacts.SearchCallCount);
        Assert.Equal(1, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_allows_active_project_defined_role_lens()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var baseMemoryFact = ProjectMemoryFact();
        var memoryFacts = new FakeMemoryFactRepository(baseMemoryFact);
        var roleDefinitions = new FakeProjectRoleDefinitionStore("implementation_lead");
        var workflow = CreateWorkflow(
            sourceEvents,
            writeStore,
            memoryFacts: memoryFacts,
            projectRoleDefinitions: roleDefinitions);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/implementation_lead/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize implementation sequencing and migration checks",
            roleId: "implementation_lead",
            baseMemoryFactId: baseMemoryFact.Id));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal(1, roleDefinitions.CallCount);
        Assert.Equal(1, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_inactive_project_defined_role_before_authorizing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var workflow = CreateWorkflow(
            sourceEvents,
            writeStore,
            accessAuthorizer,
            projectRoleDefinitions: new FakeProjectRoleDefinitionStore());

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/implementation_lead/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize implementation sequencing and migration checks",
            roleId: "implementation_lead",
            baseMemoryFactId: Guid.Parse("99999999-9999-4999-8999-999999999999")));

        Assert.False(result.IsValid);
        Assert.Contains("active project role definition", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(0, accessAuthorizer.CallCount);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_non_role_lens_proposal_in_role_lens_namespace_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "fact",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine",
            predicate: "uses",
            objectValue: "postgres"));

        Assert.False(result.IsValid);
        Assert.Contains("reserved for role-lens proposals", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(0, accessAuthorizer.CallCount);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_allows_global_role_lens_namespace_for_global_scope()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var baseMemoryFact = ProjectMemoryFact() with
        {
            ScopeType = "global",
            ScopeId = "global",
            Namespace = "/global/role-principles",
            ProjectId = null,
            OrgId = null
        };
        var memoryFacts = new FakeMemoryFactRepository(baseMemoryFact);
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "role_lens",
            scopeType: "global",
            scopeId: "global",
            namespaceValue: "/role/cto/shared",
            visibility: "role_shared",
            subject: "architecture review",
            predicate: "means_for_role",
            objectValue: "prioritize source-backed migration plans",
            roleId: "cto",
            baseMemoryFactId: baseMemoryFact.Id));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Equal("global", writeStore.Proposal.ScopeType);
        Assert.Equal("global", writeStore.Proposal.ScopeId);
        Assert.Equal("/role/cto/shared", writeStore.Proposal.Namespace);
        Assert.Equal(1, memoryFacts.FindCallCount);
        Assert.Equal(1, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_role_lens_base_fact_outside_scope_without_writing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var memoryFacts = new FakeMemoryFactRepository(ProjectMemoryFact() with
        {
            ProjectId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            ScopeId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
        });
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cto",
            baseMemoryFactId: Guid.Parse("99999999-9999-4999-8999-999999999999")));

        Assert.False(result.IsValid);
        Assert.Contains("target project", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(1, memoryFacts.FindCallCount);
        Assert.Equal(0, memoryFacts.SearchCallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_forbids_role_lens_base_fact_without_read_access()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var baseMemoryFact = ProjectMemoryFact();
        var memoryFacts = new FakeMemoryFactRepository(baseMemoryFact);
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true, readAllowed: false);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer, memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cto",
            baseMemoryFactId: baseMemoryFact.Id));

        Assert.False(result.IsValid);
        Assert.Equal(403, result.FailureStatusCode);
        Assert.Contains("read access", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(2, accessAuthorizer.CallCount);
        Assert.Equal(1, memoryFacts.FindCallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_role_lens_namespace_that_does_not_match_role_id_before_authorizing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cfo",
            baseMemoryFactId: Guid.Parse("99999999-9999-4999-8999-999999999999")));

        Assert.False(result.IsValid);
        Assert.Contains("roleId 'cfo'", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(0, accessAuthorizer.CallCount);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_role_lens_without_supported_role_id_before_authorizing()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: null,
            baseMemoryFactId: Guid.Parse("99999999-9999-4999-8999-999999999999")));

        Assert.False(result.IsValid);
        Assert.Contains("supported roleId", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(0, accessAuthorizer.CallCount);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_role_lens_without_base_fact_before_review_decision()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            confidence: 0.45m,
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cto",
            baseMemoryFactId: null));

        Assert.False(result.IsValid);
        Assert.Contains("baseMemoryFactId is required", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(0, accessAuthorizer.CallCount);
        Assert.Equal(0, sourceEvents.CallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_rejects_unknown_role_lens_base_fact_before_review_decision()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var memoryFacts = new FakeMemoryFactRepository();
        var workflow = CreateWorkflow(sourceEvents, writeStore, memoryFacts: memoryFacts);

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: $"/project/{ProjectId}/role/cto/lens",
            visibility: "project_shared",
            confidence: 0.45m,
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cto",
            baseMemoryFactId: Guid.Parse("99999999-9999-4999-8999-999999999999")));

        Assert.False(result.IsValid);
        Assert.Contains("existing memory fact", result.InvalidReason, StringComparison.Ordinal);
        Assert.Equal(1, sourceEvents.CallCount);
        Assert.Equal(1, memoryFacts.FindCallCount);
        Assert.Equal(0, writeStore.CallCount);
    }

    [Fact]
    public async Task DecideAsync_authorizes_requested_child_role_lens_namespace()
    {
        var sourceEvents = new FakeSourceEventReferenceStore(sourceEventExists: true);
        var writeStore = new FakeMemoryProposalWriteStore();
        var baseMemoryFact = ProjectMemoryFact();
        var memoryFacts = new FakeMemoryFactRepository(baseMemoryFact);
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var workflow = CreateWorkflow(sourceEvents, writeStore, accessAuthorizer, memoryFacts);
        var childNamespace = $"/project/{ProjectId}/role/cto/lens/architecture";

        var result = await workflow.DecideAsync(CreateRequest(
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: ProjectId.ToString(),
            namespaceValue: childNamespace,
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: "cto",
            baseMemoryFactId: baseMemoryFact.Id));

        Assert.True(result.IsValid);
        Assert.Equal(MemoryProposalDecisions.Stored, result.Decision?.Decision);
        Assert.Contains(childNamespace, accessAuthorizer.WriteNamespaces);
        Assert.Equal(1, writeStore.CallCount);
    }

    private static MemoryProposalWorkflow CreateWorkflow(
        ISourceEventReferenceStore sourceEvents,
        IMemoryProposalWriteStore writeStore,
        IMemoryAccessAuthorizer? accessAuthorizer = null,
        IMemoryFactRepository? memoryFacts = null,
        IProjectRoleDefinitionStore? projectRoleDefinitions = null)
    {
        return new MemoryProposalWorkflow(
            new MinimalMemoryProposalBroker(),
            writeStore,
            sourceEvents,
            memoryFacts ?? new FakeMemoryFactRepository(),
            new MemoryScopeResolver(new FakeMemoryScopeReferenceStore()),
            accessAuthorizer ?? new FakeMemoryAccessAuthorizer(allowed: true),
            projectRoleDefinitions ?? new FakeProjectRoleDefinitionStore());
    }

    private sealed class FakeProjectRoleDefinitionStore(params string[] activeProjectRoleIds) : IProjectRoleDefinitionStore
    {
        private readonly HashSet<string> activeProjectRoleIds = activeProjectRoleIds
            .Select(roleId =>
            {
                Assert.True(MemoryRoleId.TryNormalizeIdentifier(roleId, out var normalizedRoleId, out _));
                return normalizedRoleId;
            })
            .ToHashSet(StringComparer.Ordinal);

        public int CallCount { get; private set; }

        public Task<ProjectRoleDefinitionRecord> UpsertAsync(
            ProjectRoleDefinitionCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsActiveProjectRoleAsync(
            Guid projectId,
            string roleId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(ProjectId, projectId);
            Assert.True(MemoryRoleId.TryNormalizeIdentifier(roleId, out var normalizedRoleId, out _));

            return Task.FromResult(
                MemoryRoleId.IsDefaultTemplate(normalizedRoleId)
                || activeProjectRoleIds.Contains(normalizedRoleId));
        }
    }

    private static MemoryProposalWorkflowRequest CreateRequest(
        string? memoryType = "preference",
        string? scopeType = "user",
        string? visibility = "private",
        decimal? confidence = 0.95m,
        string? trustLevel = "user_scoped",
        string? sensitivity = "none",
        string? namespaceValue = null,
        string? objectValue = "concise decision logs",
        string? scopeId = null,
        string subject = "technical planning format",
        string predicate = "prefers",
        string? roleId = null,
        Guid? baseMemoryFactId = null)
    {
        var resolvedScopeId = scopeId
            ?? (string.Equals(scopeType?.Trim(), "session", StringComparison.OrdinalIgnoreCase)
                ? "session-1"
                : PrincipalId.ToString());

        return new MemoryProposalWorkflowRequest(
            PrincipalId,
            IdempotencyRecordId,
            "request-hash",
            SourceEventId,
            memoryType,
            scopeType,
            resolvedScopeId,
            namespaceValue ?? $"/user/{PrincipalId}/preferences",
            visibility,
            subject,
            predicate,
            objectValue,
            confidence,
            trustLevel,
            sensitivity,
            roleId,
            baseMemoryFactId);
    }

    private sealed class FakeSourceEventReferenceStore(
        bool sourceEventExists,
        string trustLevel = "user_scoped",
        string sensitivity = "none") : ISourceEventReferenceStore
    {
        public int CallCount { get; private set; }

        public Task<SourceEventReference?> FindForPrincipalScopeAsync(
            Guid eventId,
            Guid principalId,
            string scopeType,
            string scopeId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(SourceEventId, eventId);
            Assert.Equal(PrincipalId, principalId);

            return Task.FromResult(
                sourceEventExists
                    ? new SourceEventReference(eventId, trustLevel, sensitivity)
                    : null);
        }
    }

    private sealed class FakeMemoryProposalWriteStore(Guid? memoryId = null) : IMemoryProposalWriteStore
    {
        public int CallCount { get; private set; }
        public Guid ProposedByPrincipalId { get; private set; }
        public MemoryProposalCommand Proposal { get; private set; } = null!;
        public Guid IdempotencyRecordId { get; private set; }
        public string RequestHash { get; private set; } = string.Empty;

        public Task<MemoryProposalDecision> StoreAsync(
            Guid proposedByPrincipalId,
            MemoryProposalCommand proposal,
            Guid idempotencyRecordId,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProposedByPrincipalId = proposedByPrincipalId;
            Proposal = proposal;
            IdempotencyRecordId = idempotencyRecordId;
            RequestHash = requestHash;

            return Task.FromResult(new MemoryProposalDecision(
                MemoryProposalDecisions.Stored,
                "The proposal was stored as durable memory.",
                memoryId ?? Guid.NewGuid(),
                proposal.SourceEventId,
                proposal.CandidateKind,
                proposal.Confidence));
        }
    }

    private sealed class FakeMemoryScopeReferenceStore : IMemoryScopeReferenceStore
    {
        public Task<bool> OrganizationExistsAsync(Guid orgId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<ProjectScopeReference?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProjectScopeReference?>(new ProjectScopeReference(projectId, OrgId));
        }

        public Task<bool> PrincipalExistsAsync(
            Guid principalId,
            string? principalType = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMemoryFactRepository(params MemoryFactRecord[] memoryFacts) : IMemoryFactRepository
    {
        public int FindCallCount { get; private set; }
        public int SearchCallCount { get; private set; }

        public Task<MemoryFactRecord?> FindAsync(
            Guid memoryFactId,
            CancellationToken cancellationToken = default)
        {
            FindCallCount++;

            return Task.FromResult(memoryFacts.FirstOrDefault(memoryFact => memoryFact.Id == memoryFactId));
        }

        public Task<IReadOnlyList<MemoryFactRecord>> FindByScopeAsync(
            MemoryFactScopeQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MemoryFactRecord>> SearchAsync(
            MemoryFactSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            SearchCallCount++;

            var matches = memoryFacts
                .Where(memoryFact =>
                    memoryFact.ScopeType == query.Scope.ScopeType
                    && memoryFact.ScopeId == query.Scope.ScopeId
                    && memoryFact.MemoryType == query.MemoryType
                    && memoryFact.Status == query.Status
                    && (string.IsNullOrWhiteSpace(query.Subject)
                        || memoryFact.Subject.Contains(query.Subject, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            return Task.FromResult<IReadOnlyList<MemoryFactRecord>>(matches);
        }

        public Task<IReadOnlyList<MemoryFactRecord>> FindActiveBySubjectPredicateAsync(
            MemoryFactSubjectPredicateQuery query,
            CancellationToken cancellationToken = default)
        {
            SearchCallCount++;

            var matches = memoryFacts
                .Where(memoryFact =>
                    memoryFact.ScopeType == query.Scope.ScopeType
                    && memoryFact.ScopeId == query.Scope.ScopeId
                    && memoryFact.MemoryType == query.MemoryType
                    && memoryFact.Status == MemoryFactStatuses.Active
                    && string.Equals(memoryFact.Subject, query.Subject, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(memoryFact.Predicate, query.Predicate, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return Task.FromResult<IReadOnlyList<MemoryFactRecord>>(matches);
        }

        public Task<MemoryFactRecord> StoreAsync(
            MemoryFactWriteCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private static MemoryFactRecord ProjectMemoryFact()
    {
        return new MemoryFactRecord(
            Guid.Parse("99999999-9999-4999-8999-999999999999"),
            "project",
            ProjectId.ToString(),
            $"/project/{ProjectId}/decisions",
            UserPrincipalId: null,
            ProjectId,
            OrgId,
            RoleId: null,
            AgentPrincipalId: null,
            "decision",
            "project_shared",
            "storage engine",
            "uses",
            "postgres",
            0.95m,
            "user_scoped",
            MemoryFactStatuses.Active,
            SourceEventId,
            PrincipalId);
    }

    private sealed class FakeMemoryAccessAuthorizer(bool allowed, bool readAllowed = true) : IMemoryAccessAuthorizer
    {
        public int CallCount { get; private set; }
        public List<string> WriteNamespaces { get; } = [];

        public Task<MemoryAccessDecision> AuthorizeAsync(
            MemoryAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(PrincipalId, request.PrincipalId);
            Assert.Contains(request.Permission, new[] { MemoryAccessPermissions.Write, MemoryAccessPermissions.Read });
            Assert.False(string.IsNullOrWhiteSpace(request.Namespace));

            if (request.Permission == MemoryAccessPermissions.Write)
            {
                WriteNamespaces.Add(request.Namespace);
            }

            if (request.Permission == MemoryAccessPermissions.Read && !readAllowed)
            {
                return Task.FromResult(MemoryAccessDecision.Deny("Principal does not have read access."));
            }

            return Task.FromResult(allowed
                ? MemoryAccessDecision.Allow()
                : MemoryAccessDecision.Deny("Principal does not have write access."));
        }
    }
}
