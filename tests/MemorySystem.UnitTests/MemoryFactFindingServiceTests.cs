using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.UnitTests;

public sealed class MemoryFactFindingServiceTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid FactId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid RelatedFactId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");

    [Fact]
    public async Task QueryFactsAsync_normalizes_request_and_maps_fact_response()
    {
        var store = new FakeMemoryFactFindingStore(new MemoryFactFindingStoreResult(
            [
                FactRecord(
                    subject: "project a migrations",
                    predicate: "use",
                    objectValue: "SQL-first migrations",
                    confidence: 0.950m)
            ],
            [],
            []));
        var service = new MemoryFactFindingService(store, new FakeSourceEventLinkBuilder());

        var result = await service.QueryFactsAsync(new MemoryFactFindingQuery(
            PrincipalId,
            "  migration strategy  ",
            TargetScopeType: "PROJECT",
            TargetScopeId: ProjectId.ToString().ToUpperInvariant(),
            RoleId: "CTO",
            Namespaces: [$" /project/{ProjectId}/decisions "],
            MemoryTypes: ["Decision"],
            IncludeContradictions: true,
            IncludeExcluded: true));

        Assert.Equal("migration strategy", store.Query!.Query);
        Assert.Equal("project", store.Query.TargetScopeType);
        Assert.Equal(ProjectId.ToString(), store.Query.TargetScopeId);
        Assert.Equal("cto", store.Query.RoleId);
        Assert.Equal(8, store.Query.Limit);
        Assert.Equal([$"/project/{ProjectId}/decisions"], store.Query.Namespaces);
        Assert.Equal(["decision"], store.Query.MemoryTypes);

        var fact = Assert.Single(result.Facts);
        Assert.Equal(FactId, fact.Id);
        Assert.Equal("Project a migrations use SQL-first migrations.", fact.Claim);
        Assert.Equal(0.950m, fact.Confidence);
        Assert.Equal([SourceEventId], fact.SourceEventIds);
        Assert.Equal([$"/api/events/{SourceEventId}"], fact.SourceLinks);
        Assert.True(fact.Policy.Authorized);
        Assert.True(fact.Policy.EvidenceCurrent);
        Assert.Equal(0.950m, result.OverallConfidence);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task QueryFactsAsync_maps_contradictions_and_safe_exclusions()
    {
        var store = new FakeMemoryFactFindingStore(new MemoryFactFindingStoreResult(
            [FactRecord(confidence: 0.700m)],
            [
                new MemoryFactContradictionRecord(
                    "project a migrations",
                    "use",
                    FactId,
                    RelatedFactId,
                    MemoryFactStatuses.Superseded,
                    SourceEventId)
            ],
            [
                new MemoryFactExclusionSummary("inactive", 2),
                new MemoryFactExclusionSummary("over_limit", 0)
            ]));
        var service = new MemoryFactFindingService(store, new FakeSourceEventLinkBuilder());

        var result = await service.QueryFactsAsync(new MemoryFactFindingQuery(
            PrincipalId,
            "migrations",
            IncludeContradictions: true,
            IncludeExcluded: true,
            Limit: 3));

        var contradiction = Assert.Single(result.Contradictions);
        Assert.Equal(FactId, contradiction.CurrentFactId);
        Assert.Equal(RelatedFactId, contradiction.RelatedFactId);
        Assert.Equal(MemoryFactStatuses.Superseded, contradiction.RelatedStatus);
        Assert.Equal([$"/api/events/{SourceEventId}"], contradiction.SourceLinks);

        Assert.Contains(result.Excluded, exclusion =>
            exclusion is { Reason: "inactive", Count: 2, CountDisclosure: null });
        Assert.Contains(result.Excluded, exclusion =>
            exclusion is { Reason: "not_authorized", Count: null, CountDisclosure: "withheld" });
        Assert.DoesNotContain(result.Excluded, exclusion => exclusion is { Reason: "over_limit", Count: 0 });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryFactsAsync_rejects_blank_query(string query)
    {
        var service = new MemoryFactFindingService(
            new FakeMemoryFactFindingStore(new MemoryFactFindingStoreResult([], [], [])),
            new FakeSourceEventLinkBuilder());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, query)));
    }

    [Fact]
    public async Task QueryFactsAsync_rejects_invalid_filters()
    {
        var service = new MemoryFactFindingService(
            new FakeMemoryFactFindingStore(new MemoryFactFindingStoreResult([], [], [])),
            new FakeSourceEventLinkBuilder());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, "query", TargetScopeType: "project")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, "query", RoleId: "astronaut")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, "query", Namespaces: ["project/a"])));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, "query", MemoryTypes: ["unknown"])));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, "query", Limit: 21)));
    }

    [Fact]
    public async Task QueryFactsAsync_returns_generic_warning_when_no_active_facts_match()
    {
        var service = new MemoryFactFindingService(
            new FakeMemoryFactFindingStore(new MemoryFactFindingStoreResult([], [], [])),
            new FakeSourceEventLinkBuilder());

        var result = await service.QueryFactsAsync(new MemoryFactFindingQuery(PrincipalId, "missing"));

        Assert.Empty(result.Facts);
        Assert.Equal(0m, result.OverallConfidence);
        Assert.Equal(["No active authorized facts matched the query."], result.Warnings);
    }

    private static MemoryFactFindingRecord FactRecord(
        string subject = "memory retrieval",
        string predicate = "uses",
        string objectValue = "authorized SQL",
        decimal confidence = 0.900m)
    {
        return new MemoryFactFindingRecord(
            FactId,
            "decision",
            MemoryFactStatuses.Active,
            "project",
            ProjectId.ToString(),
            $"/project/{ProjectId}/decisions",
            subject,
            predicate,
            objectValue,
            confidence,
            "user_scoped",
            "none",
            "standard",
            "none",
            SourceEventId);
    }

    private sealed class FakeMemoryFactFindingStore(MemoryFactFindingStoreResult result) : IMemoryFactFindingStore
    {
        public MemoryFactFindingQuery? Query { get; private set; }

        public Task<MemoryFactFindingStoreResult> QueryFactsAsync(
            MemoryFactFindingQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;

            return Task.FromResult(result);
        }
    }

    private sealed class FakeSourceEventLinkBuilder : ISourceEventLinkBuilder
    {
        public string Build(Guid sourceEventId)
        {
            return $"/api/events/{sourceEventId}";
        }
    }
}
