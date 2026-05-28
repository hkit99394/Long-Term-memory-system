using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;

namespace MemorySystem.UnitTests;

public sealed class MemoryContextPacketBuilderTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public async Task BuildAsync_compacts_content_without_exceeding_limit()
    {
        var sourceId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var search = new FakeHybridSearch(
        [
            new MemoryChunkHybridSearchResult(
                Guid.NewGuid(),
                "memory_fact",
                sourceId,
                "decision",
                BaseMemoryFactId: null,
                $"/project/{Guid.Parse("33333333-3333-4333-8333-333333333333")}/decisions",
                "project",
                "33333333-3333-4333-8333-333333333333",
                "Long decision",
                new string('x', 420),
                0.75d,
                "human_approved",
                sourceEventId,
                new MemoryChunkHybridRankComponents(1, 1, 1, 1, 1))
        ]);
        var builder = new MemoryContextPacketBuilder(search, new TestSourceEventLinkBuilder());

        var packet = await builder.BuildAsync(new MemoryContextPacketQuery(PrincipalId, "decision", Limit: 1));

        var item = Assert.Single(packet.RelevantDecisions);
        Assert.Equal(360, item.Content.Length);
        Assert.EndsWith("...", item.Content, StringComparison.Ordinal);
        Assert.Equal($"/api/events/{sourceEventId}", item.SourceLink);
    }

    [Fact]
    public async Task BuildAsync_normalizes_target_scope_and_role_before_searching()
    {
        var search = new FakeHybridSearch([]);
        var builder = new MemoryContextPacketBuilder(search, new TestSourceEventLinkBuilder());
        const string projectId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";

        var packet = await builder.BuildAsync(new MemoryContextPacketQuery(
            PrincipalId,
            "decision",
            Limit: 1,
            TargetScopeType: " PROJECT ",
            TargetScopeId: projectId.ToUpperInvariant(),
            RoleId: " CTO "));

        Assert.Equal("project", packet.TargetScope?.ScopeType);
        Assert.Equal(projectId, packet.TargetScope?.ScopeId);
        Assert.Equal("cto", packet.RoleId);
        Assert.Equal("project", search.LastQuery?.TargetScopeType);
        Assert.Equal(projectId, search.LastQuery?.TargetScopeId);
        Assert.Equal("cto", search.LastQuery?.RoleId);
    }

    [Fact]
    public async Task BuildAsync_excludes_role_specific_results_for_other_roles()
    {
        var projectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var cfoLensId = Guid.NewGuid();
        var ctoLensId = Guid.NewGuid();
        var search = new FakeHybridSearch(
        [
            CreateRoleLensResult(cfoLensId, projectId, "cfo"),
            CreateRoleLensResult(ctoLensId, projectId, "cto")
        ]);
        var builder = new MemoryContextPacketBuilder(search, new TestSourceEventLinkBuilder());

        var packet = await builder.BuildAsync(new MemoryContextPacketQuery(
            PrincipalId,
            "role lens",
            Limit: 2,
            TargetScopeType: "project",
            TargetScopeId: projectId.ToString(),
            RoleId: "cto"));

        var roleMemory = Assert.Single(packet.RoleMemory);
        Assert.Equal(ctoLensId, roleMemory.SourceId);
    }

    [Fact]
    public async Task BuildAsync_keeps_org_role_results_for_matching_role()
    {
        var orgId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var ctoLensId = Guid.NewGuid();
        var search = new FakeHybridSearch(
        [
            new MemoryChunkHybridSearchResult(
                Guid.NewGuid(),
                "role_memory_lens",
                ctoLensId,
                "role_lens",
                Guid.NewGuid(),
                $"/org/{orgId}/role/cto/lens",
                "org",
                orgId.ToString(),
                "cto lens",
                "cto role lens content",
                0.75d,
                "human_approved",
                Guid.NewGuid(),
                new MemoryChunkHybridRankComponents(1, 1, 1, 1, 1))
        ]);
        var builder = new MemoryContextPacketBuilder(search, new TestSourceEventLinkBuilder());

        var packet = await builder.BuildAsync(new MemoryContextPacketQuery(
            PrincipalId,
            "role lens",
            Limit: 1,
            TargetScopeType: "org",
            TargetScopeId: orgId.ToString(),
            RoleId: "cto"));

        var roleMemory = Assert.Single(packet.RoleMemory);
        Assert.Equal(ctoLensId, roleMemory.SourceId);
    }

    private static MemoryChunkHybridSearchResult CreateRoleLensResult(Guid sourceId, Guid projectId, string roleId)
    {
        return new MemoryChunkHybridSearchResult(
            Guid.NewGuid(),
            "role_memory_lens",
            sourceId,
            "role_lens",
            Guid.NewGuid(),
            $"/project/{projectId}/role/{roleId}/lens",
            "project",
            projectId.ToString(),
            $"{roleId} lens",
            $"{roleId} role lens content",
            0.75d,
            "human_approved",
            Guid.NewGuid(),
            new MemoryChunkHybridRankComponents(1, 1, 1, 1, 1));
    }

    private sealed class FakeHybridSearch(IReadOnlyList<MemoryChunkHybridSearchResult> results) : IMemoryChunkHybridSearch
    {
        public MemoryChunkHybridSearchQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<MemoryChunkHybridSearchResult>> SearchAsync(
            MemoryChunkHybridSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;

            return Task.FromResult(results);
        }
    }

    private sealed class TestSourceEventLinkBuilder : ISourceEventLinkBuilder
    {
        public string Build(Guid sourceEventId)
        {
            return $"/api/events/{sourceEventId}";
        }
    }
}
