internal static class Scenario0001
{
    public static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid OrgId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid ProjectBId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    public static readonly Guid UserPreferenceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    public static readonly Guid ProjectDecisionEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    public static readonly Guid SharedCtoPrincipleEventId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    public static readonly Guid ProjectCtoLensEventId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    public static readonly Guid ProjectBDecisionEventId = Guid.Parse("12121212-1212-4121-8121-121212121212");

    public static readonly Guid UserPreferenceMemoryFactId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    public static readonly Guid ProjectDecisionMemoryFactId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    public static readonly Guid SharedCtoPrincipleMemoryFactId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    public static readonly Guid ProjectBDecisionMemoryFactId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");

    public static readonly Guid SharedCtoPrincipleRoleMemoryLensId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    public static readonly Guid ProjectCtoLensRoleMemoryLensId = Guid.Parse("efefefef-efef-4efe-8efe-efefefefefef");

    public static readonly Guid ProjectRoleAssignmentId = Guid.Parse("02020202-0202-4202-8202-020202020201");
    public static readonly Guid OrgRoleAssignmentId = Guid.Parse("02020202-0202-4202-8202-020202020202");

    public static readonly Guid UserPreferenceReadGrantId = Guid.Parse("01010101-0101-4101-8101-010101010101");
    public static readonly Guid UserPreferenceWriteGrantId = Guid.Parse("01010101-0101-4101-8101-010101010102");
    public static readonly Guid OrgPolicyReadGrantId = Guid.Parse("01010101-0101-4101-8101-010101010103");
    public static readonly Guid ProjectDecisionReadGrantId = Guid.Parse("01010101-0101-4101-8101-010101010104");
    public static readonly Guid GlobalRoleReadGrantId = Guid.Parse("01010101-0101-4101-8101-010101010105");
    public static readonly Guid OrgRoleLensReadGrantId = Guid.Parse("01010101-0101-4101-8101-010101010106");
    public static readonly Guid ProjectRoleLensReadGrantId = Guid.Parse("01010101-0101-4101-8101-010101010107");

    public static readonly Guid UserPreferenceChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-000000000001");
    public static readonly Guid ProjectDecisionChunkId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-000000000001");
    public static readonly Guid SharedCtoPrincipleFactChunkId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-000000000001");
    public static readonly Guid SharedCtoPrincipleLensChunkId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-000000000001");
    public static readonly Guid ProjectCtoLensChunkId = Guid.Parse("efefefef-efef-4efe-8efe-000000000001");
    public static readonly Guid ProjectBDecisionChunkId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-000000000001");

    public static readonly Guid UserPreferenceOutboxJobId = Guid.Parse("0f0f0f0f-0f0f-4f0f-8f0f-000000000001");
    public static readonly Guid ProjectDecisionOutboxJobId = Guid.Parse("0f0f0f0f-0f0f-4f0f-8f0f-000000000002");
    public static readonly Guid SharedCtoPrincipleFactOutboxJobId = Guid.Parse("0f0f0f0f-0f0f-4f0f-8f0f-000000000003");
    public static readonly Guid SharedCtoPrincipleLensOutboxJobId = Guid.Parse("0f0f0f0f-0f0f-4f0f-8f0f-000000000004");
    public static readonly Guid ProjectCtoLensOutboxJobId = Guid.Parse("0f0f0f0f-0f0f-4f0f-8f0f-000000000005");
    public static readonly Guid ProjectBDecisionOutboxJobId = Guid.Parse("0f0f0f0f-0f0f-4f0f-8f0f-000000000006");

    public static readonly Guid[] EventIds =
    [
        UserPreferenceEventId,
        ProjectDecisionEventId,
        SharedCtoPrincipleEventId,
        ProjectCtoLensEventId,
        ProjectBDecisionEventId
    ];

    public static readonly Guid[] MemoryFactIds =
    [
        UserPreferenceMemoryFactId,
        ProjectDecisionMemoryFactId,
        SharedCtoPrincipleMemoryFactId,
        ProjectBDecisionMemoryFactId
    ];

    public static readonly Guid[] RoleMemoryLensIds =
    [
        SharedCtoPrincipleRoleMemoryLensId,
        ProjectCtoLensRoleMemoryLensId
    ];

    public static readonly Guid[] ChunkIds =
    [
        UserPreferenceChunkId,
        ProjectDecisionChunkId,
        SharedCtoPrincipleFactChunkId,
        SharedCtoPrincipleLensChunkId,
        ProjectCtoLensChunkId,
        ProjectBDecisionChunkId
    ];

    public const string UserPreferenceEventContent =
        """{"message":"For technical planning, I prefer concise decision logs with a short rationale and explicit tradeoffs."}""";

    public const string ProjectDecisionEventContent =
        """{"decision":"Use SQL-first migrations plus raw Npgsql for the M1-M3 initial backend path.","rationale":"The first slice needs explicit schema control, migration repeatability, and clear authorization predicates before adding heavier ORM behavior.","tradeoffs":["More manual mapping in early repositories.","Less abstraction around permission-aware SQL."]}""";

    public const string SharedCtoPrincipleEventContent =
        """{"principle":"A CTO context packet should foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries."}""";

    public const string ProjectCtoLensEventContent =
        """{"role":"cto","baseMemoryFactId":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","lens":"For the CTO view, the SQL-first Npgsql decision should be treated as a risk-reduction move: it keeps authorization predicates visible while the schema and event provenance model are still stabilizing."}""";

    public const string ProjectBDecisionEventContent =
        """{"decision":"Use a confidential runway model for funding strategy.","sensitivity":"private project b data used only for the non-leakage check."}""";
}
