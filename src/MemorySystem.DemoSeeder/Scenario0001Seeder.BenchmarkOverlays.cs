using Npgsql;

internal static partial class Scenario0001Seeder
{
    private static async Task UpsertBenchmarkOverlaysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.FactFindingContradictionOverlayEventId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            scopeOrgId: Scenario0001.OrgId,
            scopeProjectId: Scenario0001.ProjectAId,
            scopePrincipalId: null,
            scopeRoleId: null,
            trustLevel: "human_approved",
            contentJson: Scenario0001.FactFindingContradictionOverlayEventContent,
            cancellationToken);

        await UpsertEventAsync(
            connection,
            transaction,
            Scenario0001.FactFindingRedactedOverlayEventId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            scopeOrgId: Scenario0001.OrgId,
            scopeProjectId: Scenario0001.ProjectAId,
            scopePrincipalId: null,
            scopeRoleId: null,
            trustLevel: "human_approved",
            contentJson: Scenario0001.FactFindingRedactedOverlayEventContent,
            cancellationToken);

        await UpsertMemoryFactAsync(
            connection,
            transaction,
            Scenario0001.FactFindingSupersededOverlayMemoryFactId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            $"/project/{Scenario0001.ProjectAId}/decisions",
            userPrincipalId: null,
            projectId: Scenario0001.ProjectAId,
            orgId: Scenario0001.OrgId,
            roleId: null,
            memoryType: "decision",
            visibility: "project_shared",
            subject: "M1-M3 data access",
            predicate: "uses",
            objectValue: "ORM-first migrations",
            confidence: 0.810m,
            trustLevel: "human_approved",
            sourceEventId: Scenario0001.FactFindingContradictionOverlayEventId,
            cancellationToken,
            status: "superseded");

        await UpsertMemoryFactAsync(
            connection,
            transaction,
            Scenario0001.FactFindingRedactedOverlayMemoryFactId,
            "project",
            Scenario0001.ProjectAId.ToString(),
            $"/project/{Scenario0001.ProjectAId}/decisions",
            userPrincipalId: null,
            projectId: Scenario0001.ProjectAId,
            orgId: Scenario0001.OrgId,
            roleId: null,
            memoryType: "decision",
            visibility: "project_shared",
            subject: "M1-M3 data access",
            predicate: "uses",
            objectValue: "redacted benchmark migration path should stay hidden",
            confidence: 0.500m,
            trustLevel: "human_approved",
            sourceEventId: Scenario0001.FactFindingRedactedOverlayEventId,
            cancellationToken,
            status: "redacted");
    }
}
