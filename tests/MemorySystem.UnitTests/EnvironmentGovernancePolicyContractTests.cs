using MemorySystem.Application.Governance;
using MemorySystem.Domain.Retention;
using MemorySystem.Domain.Sensitivity;

namespace MemorySystem.UnitTests;

public sealed class EnvironmentGovernancePolicyContractTests
{
    private static readonly DateOnly ValidationDate = new(2026, 6, 1);

    [Fact]
    public void Gc01_policy_contract_validates_local_ci_pilot_and_production_shapes()
    {
        foreach (var environment in EnvironmentGovernancePolicyEnvironments.All)
        {
            var policy = CreatePolicy(environment);

            var result = policy.Validate(ValidationDate);

            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
            Assert.Equal(GovernanceAuthorizationBoundaries.LocalAccessRecordsOnly, policy.AuthorizationBoundary);
            Assert.Equal(GovernanceResidencyAreas.All.Count, policy.AllowedRegions.Count);
            Assert.Equal(GovernanceDataClasses.All.Count, policy.DataClasses.Count);
            Assert.Equal(MemoryRetentionClass.All.Count, policy.RetentionWindows.Count);
            Assert.Equal(GovernanceExternalPayloadModes.Disabled, policy.ExternalPayloadPolicy.Mode);
            Assert.Empty(policy.ExternalPayloadPolicy.AllowedStoreKinds);
            Assert.NotNull(policy.ExceptionRecords);
            Assert.Contains(
                policy.EvidenceLocations,
                location => location.Id == GovernanceEvidenceLocationIds.ReleaseEvidence);
        }
    }

    [Fact]
    public void Gc01_policy_contract_allows_payload_safe_expiring_exceptions()
    {
        var policy = CreatePolicy(EnvironmentGovernancePolicyEnvironments.Pilot) with
        {
            ExceptionRecords =
            [
                new GovernanceExceptionRecord(
                    "pilot-telemetry-region-exception-2026-06",
                    [GovernanceResidencyAreas.Telemetry],
                    "project:00000000-0000-0000-0000-000000000001",
                    "platform-release-owner",
                    new DateOnly(2026, 7, 1),
                    GovernanceEvidenceLocationIds.GovernanceEvidence,
                    "Temporary telemetry routing during pilot collector validation.")
            ]
        };

        var result = policy.Validate(ValidationDate);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Gc01_policy_contract_rejects_authorization_override()
    {
        var policy = CreatePolicy(EnvironmentGovernancePolicyEnvironments.Production) with
        {
            AuthorizationBoundary = "platform_policy_grants_runtime_access"
        };

        var result = policy.Validate(ValidationDate);

        Assert.False(result.IsValid);
        Assert.Contains(
            "authorizationBoundary must be local_access_records_only.",
            result.Errors);
    }

    [Fact]
    public void Gc01_policy_contract_requires_core_governance_dimensions()
    {
        var policy = CreatePolicy(EnvironmentGovernancePolicyEnvironments.Pilot) with
        {
            AllowedRegions =
            [
                new GovernanceAllowedRegion(GovernanceResidencyAreas.Runtime, ["eu-west-2"]),
                new GovernanceAllowedRegion(GovernanceResidencyAreas.Database, ["eu-west-2"]),
                new GovernanceAllowedRegion(GovernanceResidencyAreas.Backups, ["eu-west-2"]),
                new GovernanceAllowedRegion(GovernanceResidencyAreas.Evidence, ["eu-west-2"])
            ],
            DataClasses =
            [
                new GovernanceDataClassPolicy(
                    GovernanceDataClasses.EventPayloads,
                    [GovernanceResidencyAreas.Database],
                    ["eu-west-2"])
            ],
            RetentionWindows =
            [
                new GovernanceRetentionWindow(
                    MemoryRetentionClass.Standard,
                    365,
                    90,
                    true,
                    [MemorySensitivity.Personal],
                    "minimize_payload")
            ],
            ExternalPayloadPolicy = new GovernanceExternalPayloadPolicy(
                GovernanceExternalPayloadModes.Allowed,
                [],
                "missing_evidence_location",
                false),
            EvidenceLocations =
            [
                new GovernanceEvidenceLocation(
                    GovernanceEvidenceLocationIds.ReleaseEvidence,
                    GovernanceEvidenceLocationKinds.LocalArtifact,
                    "file:///tmp/memorysystem/pilot/release",
                    "eu-west-2",
                    365,
                    "release-owner",
                    false)
            ]
        };

        var result = policy.Validate(ValidationDate);

        Assert.False(result.IsValid);
        Assert.Contains("allowedRegions must include 'telemetry'.", result.Errors);
        Assert.Contains("dataClasses must include 'backups'.", result.Errors);
        Assert.Contains("retentionWindows must include 'audit'.", result.Errors);
        Assert.Contains(
            "retentionWindows.standard.metadataRetentionDays must be at least payloadRetentionDays.",
            result.Errors);
        Assert.Contains(
            "externalPayloadPolicy.evidenceLocationId must reference an evidence location.",
            result.Errors);
        Assert.Contains(
            "externalPayloadPolicy.allowedStoreKinds is required when mode is audit_only or allowed.",
            result.Errors);
        Assert.Contains(
            "externalPayloadPolicy.deletionEvidenceRequired must be true when mode is allowed.",
            result.Errors);
        Assert.Contains(
            "evidenceLocations.release_evidence.payloadSafeOnly must be true.",
            result.Errors);
        Assert.Contains(
            "evidenceLocations.release_evidence.kind must not be local_artifact for pilot or production.",
            result.Errors);
        Assert.Contains("evidenceLocations must include 'governance_evidence'.", result.Errors);
    }

    [Fact]
    public void Gc01_policy_contract_is_documented_and_marked_done()
    {
        var root = FindRepositoryRoot();
        var contractPath = Path.Combine(root, "docs", "environment-governance-policy-gc01.md");
        var contract = File.ReadAllText(contractPath);
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("# GC-01 Environment Governance Policy Contract", contract, StringComparison.Ordinal);
        Assert.Contains("local_access_records_only", contract, StringComparison.Ordinal);
        Assert.Contains("event_payloads", contract, StringComparison.Ordinal);
        Assert.Contains("externalPayloadPolicy", contract, StringComparison.Ordinal);
        Assert.Contains("exceptionRecords", contract, StringComparison.Ordinal);
        Assert.Contains("evidenceLocations", contract, StringComparison.Ordinal);

        foreach (var environment in EnvironmentGovernancePolicyEnvironments.All)
        {
            Assert.Contains($"`{environment}`", contract, StringComparison.Ordinal);
        }

        Assert.Contains(
            "| GC-01 | P0 | Done | Define environment governance policy contract.",
            backlog,
            StringComparison.Ordinal);
        Assert.Contains("| GC-02 | P0 | Done | Add permission-drift report.", backlog, StringComparison.Ordinal);
        Assert.Contains(
            "[Environment Governance Policy GC-01](environment-governance-policy-gc01.md)",
            index,
            StringComparison.Ordinal);
        Assert.Contains("environment-governance-policy-gc01.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-08`", productPlan, StringComparison.Ordinal);
    }

    private static EnvironmentGovernancePolicy CreatePolicy(string environment)
    {
        var region = environment switch
        {
            EnvironmentGovernancePolicyEnvironments.Local => "local-workstation",
            EnvironmentGovernancePolicyEnvironments.Ci => "ci-runner",
            EnvironmentGovernancePolicyEnvironments.Pilot => "eu-west-2",
            EnvironmentGovernancePolicyEnvironments.Production => "eu-west-2",
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unsupported environment.")
        };
        var evidenceKind = environment switch
        {
            EnvironmentGovernancePolicyEnvironments.Local => GovernanceEvidenceLocationKinds.LocalArtifact,
            EnvironmentGovernancePolicyEnvironments.Ci => GovernanceEvidenceLocationKinds.CiArtifact,
            _ => GovernanceEvidenceLocationKinds.ObjectStore
        };
        var evidencePrefix = environment switch
        {
            EnvironmentGovernancePolicyEnvironments.Local => "file:///tmp/memorysystem/governance",
            EnvironmentGovernancePolicyEnvironments.Ci => "ci://memorysystem/governance",
            EnvironmentGovernancePolicyEnvironments.Pilot => "s3://memorysystem-pilot-evidence/governance",
            EnvironmentGovernancePolicyEnvironments.Production => "s3://memorysystem-production-evidence/governance",
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unsupported environment.")
        };

        return new EnvironmentGovernancePolicy(
            environment,
            "governance-policy.v1",
            GovernanceAuthorizationBoundaries.LocalAccessRecordsOnly,
            CreateAllowedRegions(region),
            CreateDataClasses(region),
            CreateRetentionWindows(),
            new GovernanceExternalPayloadPolicy(
                GovernanceExternalPayloadModes.Disabled,
                [],
                GovernanceEvidenceLocationIds.GovernanceEvidence,
                false),
            [],
            CreateEvidenceLocations(evidenceKind, evidencePrefix, region));
    }

    private static IReadOnlyList<GovernanceAllowedRegion> CreateAllowedRegions(string region)
    {
        return
        [
            new GovernanceAllowedRegion(GovernanceResidencyAreas.Runtime, [region]),
            new GovernanceAllowedRegion(GovernanceResidencyAreas.Database, [region]),
            new GovernanceAllowedRegion(GovernanceResidencyAreas.Backups, [region]),
            new GovernanceAllowedRegion(GovernanceResidencyAreas.Telemetry, [region]),
            new GovernanceAllowedRegion(GovernanceResidencyAreas.Evidence, [region])
        ];
    }

    private static IReadOnlyList<GovernanceDataClassPolicy> CreateDataClasses(string region)
    {
        return
        [
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.EventPayloads,
                [GovernanceResidencyAreas.Database, GovernanceResidencyAreas.Backups],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.MemoryFacts,
                [GovernanceResidencyAreas.Database, GovernanceResidencyAreas.Backups],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.Embeddings,
                [GovernanceResidencyAreas.Database, GovernanceResidencyAreas.Backups],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.Exports,
                [GovernanceResidencyAreas.Database, GovernanceResidencyAreas.Evidence],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.AuditRows,
                [GovernanceResidencyAreas.Database, GovernanceResidencyAreas.Evidence],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.Backups,
                [GovernanceResidencyAreas.Backups],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.Telemetry,
                [GovernanceResidencyAreas.Telemetry],
                [region]),
            new GovernanceDataClassPolicy(
                GovernanceDataClasses.BenchmarkEvidence,
                [GovernanceResidencyAreas.Evidence],
                [region])
        ];
    }

    private static IReadOnlyList<GovernanceRetentionWindow> CreateRetentionWindows()
    {
        return
        [
            new GovernanceRetentionWindow(
                MemoryRetentionClass.Ephemeral,
                7,
                30,
                true,
                [MemorySensitivity.Personal, MemorySensitivity.Secret, MemorySensitivity.Regulated],
                "delete_payload_preserve_metadata"),
            new GovernanceRetentionWindow(
                MemoryRetentionClass.Standard,
                365,
                1095,
                true,
                [MemorySensitivity.Personal, MemorySensitivity.Secret, MemorySensitivity.Regulated],
                "minimize_payload_preserve_evidence"),
            new GovernanceRetentionWindow(
                MemoryRetentionClass.Audit,
                2555,
                2555,
                true,
                [MemorySensitivity.Secret, MemorySensitivity.Regulated],
                "preserve_payload_safe_audit_record"),
            new GovernanceRetentionWindow(
                MemoryRetentionClass.LegalHold,
                null,
                3650,
                false,
                [],
                "preserve_until_hold_released"),
            new GovernanceRetentionWindow(
                MemoryRetentionClass.ErasureRequested,
                0,
                2555,
                false,
                [],
                "erase_payload_preserve_audit")
        ];
    }

    private static IReadOnlyList<GovernanceEvidenceLocation> CreateEvidenceLocations(
        string evidenceKind,
        string evidencePrefix,
        string region)
    {
        return
        [
            new GovernanceEvidenceLocation(
                GovernanceEvidenceLocationIds.ReleaseEvidence,
                evidenceKind,
                $"{evidencePrefix}/release",
                region,
                365,
                "release-owner",
                true),
            new GovernanceEvidenceLocation(
                GovernanceEvidenceLocationIds.GovernanceEvidence,
                evidenceKind,
                $"{evidencePrefix}/governance",
                region,
                2555,
                "governance-owner",
                true),
            new GovernanceEvidenceLocation(
                GovernanceEvidenceLocationIds.BackupRestoreEvidence,
                evidenceKind,
                $"{evidencePrefix}/backup-restore",
                region,
                2555,
                "platform-owner",
                true),
            new GovernanceEvidenceLocation(
                GovernanceEvidenceLocationIds.AuditExportEvidence,
                GovernanceEvidenceLocationKinds.AuditStore,
                $"{evidencePrefix}/audit-export",
                region,
                2555,
                "security-owner",
                true)
        ];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MemorySystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
