using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.AccessAuditing;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class GovernanceComplianceReleaseSmokeTests
{
    private const string TestApiKey = "test-api-key";
    private const string RawPayloadCanary = "gc08 raw source payload should not leak";
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid DisabledPrincipalId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ServicePrincipalId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid ServiceCredentialId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid OrgId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Governance_compliance_release_smoke_builds_strict_payload_safe_evidence_package()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_gc08_release_smoke_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var root = FindRepositoryRoot();
        var workDir = Path.Combine(Path.GetTempPath(), $"memorysystem-gc08-release-smoke-{Guid.NewGuid():N}");
        var governanceEvidenceDir = Path.Combine(workDir, "governance");
        var backupEvidenceDir = Path.Combine(workDir, "backup");
        var releaseEvidenceDir = Path.Combine(workDir, "release");
        var packageDir = Path.Combine(workDir, "package");
        var packageManifest = Path.Combine(packageDir, "gc08-compliance-evidence-package.json");
        var artifactIndex = Path.Combine(packageDir, "gc08-compliance-evidence-package-artifacts.ndjson");
        var packageHash = packageManifest + ".sha256";
        var packageMetrics = Path.Combine(packageDir, "compliance-evidence-package-metrics.prom");

        try
        {
            Directory.CreateDirectory(governanceEvidenceDir);
            Directory.CreateDirectory(backupEvidenceDir);
            Directory.CreateDirectory(releaseEvidenceDir);
            Directory.CreateDirectory(packageDir);

            await PrepareSmokeFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            await WriteApiEvidenceAsync(client, governanceEvidenceDir);
            await WritePolicyConfigEvidenceAsync(releaseEvidenceDir, governanceEvidenceDir, backupEvidenceDir, packageDir);
            await WriteReleaseEvidenceAsync(backupEvidenceDir, releaseEvidenceDir);

            var postgresUrl = ToPsqlConnectionString(databaseConnectionString);
            await RunScriptAsync(
                root,
                "scripts/platform-retention-minimization.sh",
                new Dictionary<string, string>
                {
                    ["MEMORYSYSTEM_POSTGRES_URL"] = postgresUrl,
                    ["MEMORYSYSTEM_ENVIRONMENT"] = "local",
                    ["MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE"] = "dry-run",
                    ["MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_DIR"] = governanceEvidenceDir,
                    ["MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_FILE"] = Path.Combine(governanceEvidenceDir, "retention-minimization-evidence.json"),
                    ["MEMORYSYSTEM_RETENTION_MINIMIZATION_METRICS_FILE"] = Path.Combine(governanceEvidenceDir, "retention-minimization-metrics.prom")
                });
            await RunScriptAsync(
                root,
                "scripts/platform-external-payload-retention-check.sh",
                new Dictionary<string, string>
                {
                    ["MEMORYSYSTEM_POSTGRES_URL"] = postgresUrl,
                    ["MEMORYSYSTEM_ENVIRONMENT"] = "local",
                    ["MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE"] = "audit-only",
                    ["MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE"] = "disabled",
                    ["MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_DIR"] = governanceEvidenceDir,
                    ["MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_FILE"] = Path.Combine(governanceEvidenceDir, "external-payload-retention-evidence.json"),
                    ["MEMORYSYSTEM_EXTERNAL_PAYLOAD_METRICS_FILE"] = Path.Combine(governanceEvidenceDir, "external-payload-retention-metrics.prom")
                });
            await RunScriptAsync(
                root,
                "scripts/platform-erasure-replay-ledger-export.sh",
                new Dictionary<string, string>
                {
                    ["MEMORYSYSTEM_POSTGRES_URL"] = postgresUrl,
                    ["MEMORYSYSTEM_ENVIRONMENT"] = "local",
                    ["MEMORYSYSTEM_BACKUP_ID"] = "gc08-release-smoke-backup",
                    ["MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_DIR"] = backupEvidenceDir,
                    ["MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_FILE"] = Path.Combine(backupEvidenceDir, "erasure-replay-ledger-evidence.json"),
                    ["MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE"] = Path.Combine(backupEvidenceDir, "erasure-replay-ledger.csv"),
                    ["MEMORYSYSTEM_ERASURE_REPLAY_METRICS_FILE"] = Path.Combine(backupEvidenceDir, "erasure-replay-ledger-metrics.prom")
                });
            await RunScriptAsync(
                root,
                "scripts/platform-compliance-evidence-package.sh",
                new Dictionary<string, string>
                {
                    ["MEMORYSYSTEM_ENVIRONMENT"] = "local",
                    ["MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE"] = "strict",
                    ["MEMORYSYSTEM_COMPLIANCE_GOVERNANCE_EVIDENCE_DIR"] = governanceEvidenceDir,
                    ["MEMORYSYSTEM_COMPLIANCE_BACKUP_EVIDENCE_DIR"] = backupEvidenceDir,
                    ["MEMORYSYSTEM_COMPLIANCE_RELEASE_EVIDENCE_DIR"] = releaseEvidenceDir,
                    ["MEMORYSYSTEM_COMPLIANCE_EVIDENCE_DIR"] = packageDir,
                    ["MEMORYSYSTEM_COMPLIANCE_PACKAGE_ID"] = "gc08-release-smoke",
                    ["MEMORYSYSTEM_COMPLIANCE_OPERATOR_ID"] = ActorPrincipalId.ToString("D"),
                    ["MEMORYSYSTEM_COMPLIANCE_RELEASE_ID"] = "gc08-release-smoke",
                    ["MEMORYSYSTEM_COMPLIANCE_PACKAGE_FILE"] = packageManifest,
                    ["MEMORYSYSTEM_COMPLIANCE_ARTIFACT_INDEX_FILE"] = artifactIndex,
                    ["MEMORYSYSTEM_COMPLIANCE_PACKAGE_HASH_FILE"] = packageHash,
                    ["MEMORYSYSTEM_COMPLIANCE_METRICS_FILE"] = packageMetrics
                });

            await AssertStrictEvidencePackageAsync(packageManifest, artifactIndex, packageHash, packageMetrics);
            await AssertPolicyEvidenceAsync(Path.Combine(releaseEvidenceDir, "environment-governance-policy-evidence.json"));
            await AssertEvidenceDoesNotLeakSensitiveValuesAsync(workDir);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
            TryDeleteDirectory(workDir);
        }
    }

    private static async Task PrepareSmokeFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ActorPrincipalId, displayName: "GC-08 Access Admin");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, TargetPrincipalId, displayName: "GC-08 Managed User");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, DisabledPrincipalId, displayName: "GC-08 Disabled User");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, ServicePrincipalId, principalType: "service", displayName: "GC-08 Service");
        await DisablePrincipalAsync(connectionString, DisabledPrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgId, ProjectId);
        await ApiDatabaseTestSupport.InsertOrganizationMembershipAsync(connectionString, OrgId, ActorPrincipalId, "owner");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, ActorPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, TargetPrincipalId, "admin");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, DisabledPrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(connectionString, TargetPrincipalId, "cto", "global");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId:D}",
            "admin",
            principalId: ActorPrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId:D}",
            "admin",
            principalId: TargetPrincipalId);
        await InsertIdentityBindingAsync(connectionString);
        await InsertServiceAccountAsync(connectionString);

        var retentionCandidateEventId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
        var redactedTargetEventId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
        var redactionSourceEventId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000003");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            retentionCandidateEventId,
            ActorPrincipalId,
            "project",
            ProjectId.ToString("D"),
            scopeOrgId: OrgId,
            scopeProjectId: ProjectId,
            trustLevel: "human_approved",
            sensitivity: "personal",
            retentionClass: "standard");
        await UpdateEventContentAndCreatedAtAsync(
            connectionString,
            retentionCandidateEventId,
            RawPayloadCanary,
            DateTimeOffset.UtcNow.AddDays(-120));

        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            redactedTargetEventId,
            ActorPrincipalId,
            "project",
            ProjectId.ToString("D"),
            scopeOrgId: OrgId,
            scopeProjectId: ProjectId,
            trustLevel: "human_approved",
            sensitivity: "secret",
            retentionClass: "erasure_requested",
            redactionStatus: "erased");
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            redactionSourceEventId,
            ActorPrincipalId,
            "project",
            ProjectId.ToString("D"),
            scopeOrgId: OrgId,
            scopeProjectId: ProjectId,
            trustLevel: "system_trusted",
            sensitivity: "none",
            retentionClass: "audit");
        await InsertMemoryRedactionAsync(connectionString, redactedTargetEventId, redactionSourceEventId);
        await InsertAccessAuditEvidenceAsync(connectionString);
    }

    private static async Task WriteApiEvidenceAsync(HttpClient client, string governanceEvidenceDir)
    {
        using var driftResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
            "/api/admin/access/permission-drift",
            new
            {
                scopeType = "project",
                scopeId = ProjectId,
                namespacePrefix = $"/project/{ProjectId:D}",
                staleAfterDays = 30,
                maxPreviewPrincipals = 10
            }));
        await WriteSuccessfulResponseAsync(driftResponse, Path.Combine(governanceEvidenceDir, "permission-drift-report.json"));

        using var auditResponse = await client.SendAsync(CreateAuthenticatedJsonRequest(
            "/api/admin/audit-exports",
            new
            {
                occurredFrom = DateTimeOffset.UtcNow.AddMinutes(-15),
                occurredTo = DateTimeOffset.UtcNow.AddMinutes(15),
                scopeType = "project",
                scopeId = ProjectId,
                limit = 100
            }));
        await WriteSuccessfulResponseAsync(auditResponse, Path.Combine(governanceEvidenceDir, "audit-export.ndjson"));

        var namespacePrefix = Uri.EscapeDataString($"/project/{ProjectId:D}");
        var retentionReportPath =
            $"/api/admin/governance/retention-report?scopeType=project&scopeId={ProjectId:D}&namespacePrefix={namespacePrefix}&limit=100";
        using var retentionReportResponse = await client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, retentionReportPath));
        await WriteSuccessfulResponseAsync(retentionReportResponse, Path.Combine(governanceEvidenceDir, "retention-report.json"));

        using var legalHoldsResponse = await client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/admin/governance/legal-holds?status=active&limit=100"));
        await WriteSuccessfulResponseAsync(legalHoldsResponse, Path.Combine(governanceEvidenceDir, "legal-hold-summary.json"));
    }

    private static async Task WritePolicyConfigEvidenceAsync(
        string releaseEvidenceDir,
        string governanceEvidenceDir,
        string backupEvidenceDir,
        string packageDir)
    {
        await WriteJsonEvidenceAsync(
            Path.Combine(releaseEvidenceDir, "environment-governance-policy-evidence.json"),
            new
            {
                schemaVersion = 1,
                kind = "memorysystem.environment_governance_policy_evidence",
                environment = "local",
                authorizationBoundary = "local_access_records_only",
                payloadSafe = true,
                allowedRegions = new[] { "developer-workstation" },
                evidenceLocations = new
                {
                    governance = governanceEvidenceDir,
                    backupRestore = backupEvidenceDir,
                    release = releaseEvidenceDir,
                    compliancePackage = packageDir
                },
                verifiedChecks = new[]
                {
                    "policy_config",
                    "permission_drift",
                    "audit_export",
                    "retention_dry_run",
                    "erasure_replay",
                    "strict_evidence_manifest"
                }
            });
    }

    private static async Task WriteReleaseEvidenceAsync(string backupEvidenceDir, string releaseEvidenceDir)
    {
        await WriteJsonEvidenceAsync(
            Path.Combine(backupEvidenceDir, "backup-export-evidence.json"),
            BasicEvidence("memorysystem.backup_export_evidence", "backup_export", "succeeded"));
        await WriteJsonEvidenceAsync(
            Path.Combine(backupEvidenceDir, "restore-validation-evidence.json"),
            BasicEvidence("memorysystem.restore_validation_evidence", "restore_validation", "succeeded"));
        await WriteJsonEvidenceAsync(
            Path.Combine(releaseEvidenceDir, "release-checklist-evidence.json"),
            BasicEvidence("memorysystem.release_checklist_evidence", "release_checklist", "succeeded"));
        await WriteJsonEvidenceAsync(
            Path.Combine(releaseEvidenceDir, "benchmark-release-gate.json"),
            BasicEvidence("memorysystem.benchmark_release_gate", "benchmark_release_gate", "passed"));
        await WriteJsonEvidenceAsync(
            Path.Combine(releaseEvidenceDir, "alert-route-smoke.json"),
            BasicEvidence("memorysystem.alert_route_smoke", "alert_route_smoke", "succeeded"));
    }

    private static object BasicEvidence(string kind, string evidenceType, string status)
    {
        return new
        {
            schemaVersion = 1,
            kind,
            evidenceType,
            environment = "local",
            releaseId = "gc08-release-smoke",
            payloadSafe = true,
            status,
            generatedAtUtc = DateTimeOffset.UtcNow,
            omittedFields = new[]
            {
                "events.content",
                "memory_facts.object",
                "memory_chunks.content",
                "memory_reviews.notes",
                "raw_query",
                "provider_credentials"
            }
        };
    }

    private static async Task AssertStrictEvidencePackageAsync(
        string packageManifest,
        string artifactIndex,
        string packageHash,
        string packageMetrics)
    {
        Assert.True(File.Exists(packageManifest));
        Assert.True(File.Exists(artifactIndex));
        Assert.True(File.Exists(packageHash));
        Assert.True(File.Exists(packageMetrics));

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(packageManifest));
        var root = manifest.RootElement;
        Assert.Equal("memorysystem.compliance_evidence_package", root.GetProperty("kind").GetString());
        Assert.Equal("strict", root.GetProperty("mode").GetString());
        Assert.Equal("succeeded", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("payloadSafe").GetBoolean());
        Assert.Equal(0, root.GetProperty("missingRequiredArtifactCount").GetInt32());
        Assert.Equal(12, root.GetProperty("presentArtifactCount").GetInt32());

        var artifactIds = root.GetProperty("artifacts")
            .EnumerateArray()
            .Select(artifact => artifact.GetProperty("id").GetString())
            .ToArray();

        foreach (var expectedArtifact in new[]
                 {
                     "audit_export",
                     "retention_report",
                     "legal_hold_summary",
                     "permission_drift_report",
                     "retention_minimization",
                     "external_payload_retention",
                     "erasure_replay",
                     "backup_export",
                     "restore_validation",
                     "release_checklist",
                     "benchmark_release_gate",
                     "alert_route_smoke"
                 })
        {
            Assert.Contains(expectedArtifact, artifactIds);
        }

        var artifactLines = await File.ReadAllLinesAsync(artifactIndex);
        Assert.Equal(12, artifactLines.Length);
        Assert.All(artifactLines, line =>
        {
            using var artifact = JsonDocument.Parse(line);
            Assert.Equal("present", artifact.RootElement.GetProperty("status").GetString());
            Assert.True(artifact.RootElement.GetProperty("required").GetBoolean());
        });

        var metrics = await File.ReadAllTextAsync(packageMetrics);
        Assert.Contains("memorysystem_compliance_evidence_package_success", metrics, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_missing_required_artifacts", metrics, StringComparison.Ordinal);
    }

    private static async Task AssertPolicyEvidenceAsync(string policyEvidenceFile)
    {
        using var policy = JsonDocument.Parse(await File.ReadAllTextAsync(policyEvidenceFile));
        Assert.Equal(
            "memorysystem.environment_governance_policy_evidence",
            policy.RootElement.GetProperty("kind").GetString());
        Assert.Equal(
            "local_access_records_only",
            policy.RootElement.GetProperty("authorizationBoundary").GetString());
        Assert.True(policy.RootElement.GetProperty("payloadSafe").GetBoolean());
    }

    private static async Task AssertEvidenceDoesNotLeakSensitiveValuesAsync(string workDir)
    {
        var evidenceFiles = Directory.EnumerateFiles(workDir, "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".prom", StringComparison.OrdinalIgnoreCase));

        foreach (var evidenceFile in evidenceFiles)
        {
            var content = await File.ReadAllTextAsync(evidenceFile);
            Assert.DoesNotContain(RawPayloadCanary, content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gc08.target@example.test", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://idp.gc08.example.test", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sha256:gc08-service-key", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task WriteSuccessfulResponseAsync(HttpResponseMessage response, string path)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await File.WriteAllTextAsync(path, body);
    }

    private static async Task WriteJsonEvidenceAsync(string path, object evidence)
    {
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task RunScriptAsync(
        string root,
        string script,
        IReadOnlyDictionary<string, string> environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(script);

        foreach (var variable in environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {script}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{script} failed with exit code {process.ExitCode}.\nSTDOUT:\n{output}\nSTDERR:\n{error}");
        }
    }

    private static async Task DisablePrincipalAsync(string connectionString, Guid principalId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE principals
            SET status = 'disabled'
            WHERE id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertIdentityBindingAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO identity_bindings (
                id,
                provider,
                issuer,
                subject,
                principal_id,
                status,
                external_display_name,
                external_email,
                external_tenant_id,
                provider_metadata,
                last_seen_at
            )
            VALUES (
                @binding_id,
                'oidc',
                'https://idp.gc08.example.test',
                'raw-subject-should-not-leak',
                @principal_id,
                'active',
                'GC08 Target',
                'gc08.target@example.test',
                'tenant-gc08',
                @provider_metadata,
                now() - interval '120 days'
            );
            """,
            connection);
        command.Parameters.AddWithValue("binding_id", Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
        command.Parameters.AddWithValue("principal_id", TargetPrincipalId);
        command.Parameters.Add("provider_metadata", NpgsqlDbType.Jsonb).Value = """{"source":"gc-08-test"}""";

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertServiceAccountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_accounts (
                principal_id,
                owner_project_id,
                owner_principal_id,
                allowed_auth_method,
                review_due_at,
                expires_at,
                created_by_principal_id
            )
            VALUES (
                @service_principal_id,
                @project_id,
                @actor_principal_id,
                'api_key',
                now() - interval '10 days',
                now() + interval '60 days',
                @actor_principal_id
            );

            INSERT INTO service_account_credentials (
                id,
                service_principal_id,
                credential_label,
                auth_method,
                credential_fingerprint,
                status,
                review_due_at,
                expires_at,
                last_used_at,
                created_by_principal_id
            )
            VALUES (
                @service_credential_id,
                @service_principal_id,
                'gc-08-api-key',
                'api_key',
                'sha256:gc08-service-key',
                'active',
                now() - interval '5 days',
                now() - interval '1 day',
                NULL,
                @actor_principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("service_principal_id", ServicePrincipalId);
        command.Parameters.AddWithValue("service_credential_id", ServiceCredentialId);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateEventContentAndCreatedAtAsync(
        string connectionString,
        Guid eventId,
        string message,
        DateTimeOffset createdAt)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE events
            SET content = @content,
                created_at = @created_at
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value =
            $$"""{"message":"{{message}}"}""";
        command.Parameters.AddWithValue("created_at", createdAt);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertMemoryRedactionAsync(
        string connectionString,
        Guid targetEventId,
        Guid sourceEventId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_redactions (
                id,
                target_type,
                target_id,
                redaction_type,
                reason,
                requested_by_principal_id,
                source_event_id,
                created_at
            )
            VALUES (
                @redaction_id,
                'event',
                @target_event_id,
                'redact',
                'GC-08 release smoke replay evidence.',
                @actor_principal_id,
                @source_event_id,
                now() - interval '1 day'
            );
            """,
            connection);
        command.Parameters.AddWithValue("redaction_id", Guid.NewGuid());
        command.Parameters.AddWithValue("target_event_id", targetEventId);
        command.Parameters.AddWithValue("actor_principal_id", ActorPrincipalId);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAccessAuditEvidenceAsync(string connectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new PostgresAccessAuditEventStore(dataSource);

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.Authentication,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: ActorPrincipalId,
            PrincipalType: "human",
            AuthMethod: AuthenticationMethods.ApiKey,
            CredentialId: TestApiKey,
            RequestMethod: "GET",
            RequestPath: "/api/memory/context"));

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.ProjectMembershipChange,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: ActorPrincipalId,
            TargetPrincipalId: TargetPrincipalId,
            ScopeType: "project",
            ScopeId: ProjectId.ToString("D"),
            ResourceType: "project_membership",
            ResourceId: $"{ProjectId:D}:{TargetPrincipalId:D}",
            Metadata: new Dictionary<string, string?> { ["accessLevel"] = "admin" }));

        await store.RecordAsync(new AccessAuditEventCommand(
            AccessAuditActionTypes.NamespaceGrantChange,
            AccessAuditOutcomes.Succeeded,
            ActorPrincipalId: ActorPrincipalId,
            TargetPrincipalId: TargetPrincipalId,
            NamespacePrefix: $"/project/{ProjectId:D}",
            Permission: MemoryAccessPermissions.Admin,
            ResourceType: "memory_access_grant",
            ResourceId: Guid.NewGuid().ToString("D")));
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, ActorPrincipalId.ToString("D"));
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", TestApiKey);

        return request;
    }

    private static HttpRequestMessage CreateAuthenticatedJsonRequest(string path, object body)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body);

        return request;
    }

    private static string ToPsqlConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var username = builder.Username;
        var passwordValue = builder.Password;
        var databaseName = builder.Database;
        var host = builder.Host;
        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(databaseName)
            || string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("The PostgreSQL smoke connection string must include host, username, and database.");
        }

        var user = Uri.EscapeDataString(username);
        var password = Uri.EscapeDataString(passwordValue ?? string.Empty);
        var database = Uri.EscapeDataString(databaseName);
        return $"postgresql://{user}:{password}@{host}:{builder.Port}/{database}";
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
