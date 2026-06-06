using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.Events;
using MemorySystem.Application.Scopes;

namespace MemorySystem.UnitTests;

public sealed class EventAppendWorkflowTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid IdempotencyRecordId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task AppendAsync_rejects_external_payload_uri_when_no_scheme_or_prefix_is_allowed()
    {
        var eventStore = new CapturingEventStore();
        var workflow = CreateWorkflow(eventStore, ExternalPayloadUriPolicy.Disabled);

        var result = await workflow.AppendAsync(CreateRequest("https://payloads.example.com/events/1.json"));

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.FailureStatusCode);
        Assert.Contains("externalPayloadUri", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(0, eventStore.CallCount);
    }

    [Fact]
    public async Task AppendAsync_allows_external_payload_uri_matching_configured_prefix()
    {
        var eventStore = new CapturingEventStore();
        var workflow = CreateWorkflow(
            eventStore,
            new ExternalPayloadUriPolicy(
                ["https"],
                ["https://payloads.example.com/memory/"],
                AllowLocalFileUris: false));

        var result = await workflow.AppendAsync(CreateRequest(" https://payloads.example.com/memory/event-1.json "));

        Assert.True(result.Succeeded);
        Assert.Equal(1, eventStore.CallCount);
        Assert.Equal("https://payloads.example.com/memory/event-1.json", eventStore.Command?.ExternalPayloadUri);
    }

    [Fact]
    public async Task AppendAsync_rejects_file_external_payload_uri_when_local_files_are_not_allowed()
    {
        var eventStore = new CapturingEventStore();
        var workflow = CreateWorkflow(
            eventStore,
            new ExternalPayloadUriPolicy(
                ["file"],
                ["file:///tmp/memorysystem/"],
                AllowLocalFileUris: false));

        var result = await workflow.AppendAsync(CreateRequest("file:///tmp/memorysystem/event-1.json"));

        Assert.False(result.Succeeded);
        Assert.Contains("file://", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(0, eventStore.CallCount);
    }

    [Fact]
    public async Task AppendAsync_allows_configured_file_external_payload_uri_in_local_file_mode()
    {
        var eventStore = new CapturingEventStore();
        var workflow = CreateWorkflow(
            eventStore,
            new ExternalPayloadUriPolicy(
                ["file"],
                ["file:///tmp/memorysystem/"],
                AllowLocalFileUris: true));

        var result = await workflow.AppendAsync(CreateRequest("file:///tmp/memorysystem/event-1.json"));

        Assert.True(result.Succeeded);
        Assert.Equal("file:///tmp/memorysystem/event-1.json", eventStore.Command?.ExternalPayloadUri);
    }

    private static EventAppendWorkflow CreateWorkflow(
        CapturingEventStore eventStore,
        ExternalPayloadUriPolicy externalPayloadUriPolicy)
    {
        return new EventAppendWorkflow(
            eventStore,
            new SuccessfulScopeResolver(),
            new AllowingAccessAuthorizer(),
            externalPayloadUriPolicy);
    }

    private static EventAppendWorkflowRequest CreateRequest(string? externalPayloadUri)
    {
        using var document = JsonDocument.Parse("""{"message":"hello"}""");

        return new EventAppendWorkflowRequest(
            PrincipalId,
            IdempotencyRecordId,
            "request-hash",
            PrincipalId,
            ConversationId: null,
            AgentPrincipalId: null,
            RoleId: null,
            EventType: "user_message",
            ScopeType: "user",
            ScopeId: PrincipalId.ToString("D"),
            ScopeOrgId: null,
            TrustLevel: null,
            RetentionClass: null,
            Sensitivity: null,
            externalPayloadUri,
            document.RootElement.Clone());
    }

    private sealed class CapturingEventStore : IEventStore
    {
        public int CallCount { get; private set; }
        public AppendEventCommand? Command { get; private set; }

        public Task<AppendEventResult> AppendAsync(
            AppendEventCommand command,
            Guid idempotencyRecordId,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Command = command;

            return Task.FromResult(new AppendEventResult(Guid.NewGuid(), DateTimeOffset.UtcNow));
        }
    }

    private sealed class SuccessfulScopeResolver : IMemoryScopeResolver
    {
        public Task<MemoryScopeResolveResult> ResolveEventScopeAsync(
            MemoryEventScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryScopeResolveResult.Success(
                new MemoryScopeResolution(
                    request.ScopeType ?? "user",
                    request.ScopeId ?? PrincipalId.ToString("D"),
                    PrincipalId: PrincipalId)));
        }

        public Task<MemoryScopeResolveResult> ResolveProposalScopeAsync(
            MemoryProposalScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryScopeResolveResult.Failure("not used"));
        }
    }

    private sealed class AllowingAccessAuthorizer : IMemoryAccessAuthorizer
    {
        public Task<MemoryAccessDecision> AuthorizeAsync(
            MemoryAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryAccessDecision.Allow());
        }
    }
}
