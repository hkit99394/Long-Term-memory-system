using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.UnitTests;

public sealed class MemoryFactReadServiceTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid MemoryFactId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid SourceEventId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [Fact]
    public async Task ReadAsync_returns_memory_fact_when_read_access_is_allowed()
    {
        var store = new FakeMemoryFactReadStore(ProjectMemoryFact());
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var service = new MemoryFactReadService(store, accessAuthorizer);

        var result = await service.ReadAsync(PrincipalId, MemoryFactId);

        Assert.True(result.Found);
        Assert.Equal(MemoryFactId, result.MemoryFact?.Id);
        Assert.Equal(1, accessAuthorizer.CallCount);
    }

    [Fact]
    public async Task ReadAsync_returns_not_found_when_read_access_is_denied()
    {
        var store = new FakeMemoryFactReadStore(ProjectMemoryFact());
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: false);
        var service = new MemoryFactReadService(store, accessAuthorizer);

        var result = await service.ReadAsync(PrincipalId, MemoryFactId);

        Assert.False(result.Found);
        Assert.Null(result.MemoryFact);
        Assert.Equal(1, accessAuthorizer.CallCount);
    }

    [Fact]
    public async Task ReadAsync_returns_not_found_without_authorizing_missing_memory_fact()
    {
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var service = new MemoryFactReadService(new FakeMemoryFactReadStore(memoryFact: null), accessAuthorizer);

        var result = await service.ReadAsync(PrincipalId, MemoryFactId);

        Assert.False(result.Found);
        Assert.Equal(0, accessAuthorizer.CallCount);
    }

    [Theory]
    [InlineData(MemoryFactStatuses.Tentative)]
    [InlineData(MemoryFactStatuses.Superseded)]
    [InlineData(MemoryFactStatuses.Contradicted)]
    [InlineData(MemoryFactStatuses.Expired)]
    [InlineData(MemoryFactStatuses.Deleted)]
    [InlineData(MemoryFactStatuses.Redacted)]
    public async Task ReadAsync_returns_not_found_without_authorizing_inactive_statuses(string status)
    {
        var accessAuthorizer = new FakeMemoryAccessAuthorizer(allowed: true);
        var service = new MemoryFactReadService(
            new FakeMemoryFactReadStore(ProjectMemoryFact() with
            {
                Status = status
            }),
            accessAuthorizer);

        var result = await service.ReadAsync(PrincipalId, MemoryFactId);

        Assert.False(result.Found);
        Assert.Equal(0, accessAuthorizer.CallCount);
    }

    private static MemoryFactRecord ProjectMemoryFact()
    {
        return new MemoryFactRecord(
            MemoryFactId,
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
            "storage",
            "uses",
            "postgres",
            0.95m,
            "active",
            SourceEventId,
            PrincipalId);
    }

    private sealed class FakeMemoryFactReadStore(MemoryFactRecord? memoryFact) : IMemoryFactRepository
    {
        public Task<MemoryFactRecord?> FindAsync(
            Guid memoryFactId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(MemoryFactId, memoryFactId);

            return Task.FromResult(memoryFact);
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
            throw new NotSupportedException();
        }

        public Task<MemoryFactRecord> StoreAsync(
            MemoryFactWriteCommand command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMemoryAccessAuthorizer(bool allowed) : IMemoryAccessAuthorizer
    {
        public int CallCount { get; private set; }

        public Task<MemoryAccessDecision> AuthorizeAsync(
            MemoryAccessRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(PrincipalId, request.PrincipalId);
            Assert.Equal(MemoryAccessPermissions.Read, request.Permission);
            Assert.Equal("project", request.Scope.ScopeType);
            Assert.Equal(ProjectId.ToString(), request.Scope.ScopeId);
            Assert.Equal(ProjectId, request.Scope.ProjectId);
            Assert.Equal(OrgId, request.Scope.OrgId);
            Assert.Equal($"/project/{ProjectId}/decisions", request.Namespace);

            return Task.FromResult(allowed
                ? MemoryAccessDecision.Allow()
                : MemoryAccessDecision.Deny("Principal does not have read access."));
        }
    }
}
