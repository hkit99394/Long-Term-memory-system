using MemorySystem.Domain.Retention;
using MemorySystem.Domain.Sensitivity;

namespace MemorySystem.Application.Governance;

public static class EnvironmentGovernancePolicyEnvironments
{
    public const string Local = "local";
    public const string Ci = "ci";
    public const string Pilot = "pilot";
    public const string Production = "production";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Local,
        Ci,
        Pilot,
        Production
    };
}

public static class GovernanceAuthorizationBoundaries
{
    public const string LocalAccessRecordsOnly = "local_access_records_only";
}

public static class GovernanceResidencyAreas
{
    public const string Runtime = "runtime";
    public const string Database = "database";
    public const string Backups = "backups";
    public const string Telemetry = "telemetry";
    public const string Evidence = "evidence";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Runtime,
        Database,
        Backups,
        Telemetry,
        Evidence
    };
}

public static class GovernanceDataClasses
{
    public const string EventPayloads = "event_payloads";
    public const string MemoryFacts = "memory_facts";
    public const string Embeddings = "embeddings";
    public const string Exports = "exports";
    public const string AuditRows = "audit_rows";
    public const string Backups = "backups";
    public const string Telemetry = "telemetry";
    public const string BenchmarkEvidence = "benchmark_evidence";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        EventPayloads,
        MemoryFacts,
        Embeddings,
        Exports,
        AuditRows,
        Backups,
        Telemetry,
        BenchmarkEvidence
    };
}

public static class GovernanceExternalPayloadModes
{
    public const string Disabled = "disabled";
    public const string AuditOnly = "audit_only";
    public const string Allowed = "allowed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Disabled,
        AuditOnly,
        Allowed
    };
}

public static class GovernanceEvidenceLocationIds
{
    public const string ReleaseEvidence = "release_evidence";
    public const string GovernanceEvidence = "governance_evidence";
    public const string BackupRestoreEvidence = "backup_restore_evidence";
    public const string AuditExportEvidence = "audit_export_evidence";

    public static readonly IReadOnlySet<string> Required = new HashSet<string>(StringComparer.Ordinal)
    {
        ReleaseEvidence,
        GovernanceEvidence,
        BackupRestoreEvidence,
        AuditExportEvidence
    };
}

public static class GovernanceEvidenceLocationKinds
{
    public const string LocalArtifact = "local_artifact";
    public const string CiArtifact = "ci_artifact";
    public const string ObjectStore = "object_store";
    public const string AuditStore = "audit_store";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LocalArtifact,
        CiArtifact,
        ObjectStore,
        AuditStore
    };
}

public sealed record EnvironmentGovernancePolicy(
    string Environment,
    string Version,
    string AuthorizationBoundary,
    IReadOnlyList<GovernanceAllowedRegion> AllowedRegions,
    IReadOnlyList<GovernanceDataClassPolicy> DataClasses,
    IReadOnlyList<GovernanceRetentionWindow> RetentionWindows,
    GovernanceExternalPayloadPolicy ExternalPayloadPolicy,
    IReadOnlyList<GovernanceExceptionRecord> ExceptionRecords,
    IReadOnlyList<GovernanceEvidenceLocation> EvidenceLocations)
{
    public EnvironmentGovernancePolicyValidationResult Validate(DateOnly? today = null)
    {
        return EnvironmentGovernancePolicyValidator.Validate(this, today);
    }
}

public sealed record GovernanceAllowedRegion(
    string Area,
    IReadOnlyList<string> Regions);

public sealed record GovernanceDataClassPolicy(
    string DataClass,
    IReadOnlyList<string> ResidencyAreas,
    IReadOnlyList<string> Regions);

public sealed record GovernanceRetentionWindow(
    string RetentionClass,
    int? PayloadRetentionDays,
    int MetadataRetentionDays,
    bool LegalHoldOverrideAllowed,
    IReadOnlyList<string> StricterSensitivityClasses,
    string ActionOnExpiry);

public sealed record GovernanceExternalPayloadPolicy(
    string Mode,
    IReadOnlyList<string> AllowedStoreKinds,
    string EvidenceLocationId,
    bool DeletionEvidenceRequired);

public sealed record GovernanceExceptionRecord(
    string Id,
    IReadOnlyList<string> Areas,
    string Scope,
    string ApprovedBy,
    DateOnly ExpiresOn,
    string EvidenceLocationId,
    string Reason);

public sealed record GovernanceEvidenceLocation(
    string Id,
    string Kind,
    string Uri,
    string Region,
    int RetentionDays,
    string AccessOwner,
    bool PayloadSafeOnly);

public sealed record EnvironmentGovernancePolicyValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static EnvironmentGovernancePolicyValidationResult Valid { get; } = new(true, []);

    public static EnvironmentGovernancePolicyValidationResult Invalid(IReadOnlyList<string> errors)
    {
        return new EnvironmentGovernancePolicyValidationResult(false, errors);
    }
}

public static class EnvironmentGovernancePolicyValidator
{
    public static EnvironmentGovernancePolicyValidationResult Validate(
        EnvironmentGovernancePolicy? policy,
        DateOnly? today = null)
    {
        if (policy is null)
        {
            return EnvironmentGovernancePolicyValidationResult.Invalid(["policy is required."]);
        }

        var errors = new List<string>();
        var effectiveToday = today ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        RequireSupported(
            policy.Environment,
            EnvironmentGovernancePolicyEnvironments.All,
            "environment",
            errors);

        if (string.IsNullOrWhiteSpace(policy.Version))
        {
            errors.Add("version is required.");
        }

        if (!string.Equals(
                policy.AuthorizationBoundary,
                GovernanceAuthorizationBoundaries.LocalAccessRecordsOnly,
                StringComparison.Ordinal))
        {
            errors.Add("authorizationBoundary must be local_access_records_only.");
        }

        var regionsByArea = ValidateAllowedRegions(policy.AllowedRegions, errors);
        var evidenceLocationIds = ValidateEvidenceLocations(
            policy.Environment,
            policy.EvidenceLocations,
            errors);

        ValidateDataClasses(policy.DataClasses, regionsByArea, errors);
        ValidateRetentionWindows(policy.RetentionWindows, errors);
        ValidateExternalPayloadPolicy(
            policy.ExternalPayloadPolicy,
            evidenceLocationIds,
            errors);
        ValidateExceptionRecords(
            policy.ExceptionRecords,
            evidenceLocationIds,
            errors,
            effectiveToday);

        return errors.Count == 0
            ? EnvironmentGovernancePolicyValidationResult.Valid
            : EnvironmentGovernancePolicyValidationResult.Invalid(errors);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> ValidateAllowedRegions(
        IReadOnlyList<GovernanceAllowedRegion>? allowedRegions,
        ICollection<string> errors)
    {
        var regionsByArea = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        if (allowedRegions is null || allowedRegions.Count == 0)
        {
            errors.Add("allowedRegions must include runtime, database, backups, telemetry, and evidence areas.");
            return regionsByArea;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var allowedRegion in allowedRegions)
        {
            if (!RequireSupported(allowedRegion.Area, GovernanceResidencyAreas.All, "allowedRegions.area", errors))
            {
                continue;
            }

            if (!seen.Add(allowedRegion.Area))
            {
                errors.Add($"allowedRegions contains duplicate area '{allowedRegion.Area}'.");
            }

            var regions = ValidateNonEmptyValues(allowedRegion.Regions, $"allowedRegions.{allowedRegion.Area}.regions", errors);
            regionsByArea[allowedRegion.Area] = regions;
        }

        foreach (var requiredArea in GovernanceResidencyAreas.All)
        {
            if (!seen.Contains(requiredArea))
            {
                errors.Add($"allowedRegions must include '{requiredArea}'.");
            }
        }

        return regionsByArea;
    }

    private static void ValidateDataClasses(
        IReadOnlyList<GovernanceDataClassPolicy>? dataClasses,
        IReadOnlyDictionary<string, IReadOnlySet<string>> regionsByArea,
        ICollection<string> errors)
    {
        if (dataClasses is null || dataClasses.Count == 0)
        {
            errors.Add("dataClasses must include every governance data class.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dataClass in dataClasses)
        {
            if (!RequireSupported(dataClass.DataClass, GovernanceDataClasses.All, "dataClasses.dataClass", errors))
            {
                continue;
            }

            if (!seen.Add(dataClass.DataClass))
            {
                errors.Add($"dataClasses contains duplicate data class '{dataClass.DataClass}'.");
            }

            var residencyAreas = ValidateSupportedValues(
                dataClass.ResidencyAreas,
                GovernanceResidencyAreas.All,
                $"dataClasses.{dataClass.DataClass}.residencyAreas",
                errors);
            var regions = ValidateNonEmptyValues(
                dataClass.Regions,
                $"dataClasses.{dataClass.DataClass}.regions",
                errors);

            foreach (var region in regions)
            {
                var regionAllowed = residencyAreas.Any(area =>
                    regionsByArea.TryGetValue(area, out var allowedRegions)
                    && allowedRegions.Contains(region));

                if (!regionAllowed)
                {
                    errors.Add(
                        $"dataClasses.{dataClass.DataClass}.regions contains '{region}' outside its allowed residency areas.");
                }
            }
        }

        foreach (var requiredDataClass in GovernanceDataClasses.All)
        {
            if (!seen.Contains(requiredDataClass))
            {
                errors.Add($"dataClasses must include '{requiredDataClass}'.");
            }
        }
    }

    private static void ValidateRetentionWindows(
        IReadOnlyList<GovernanceRetentionWindow>? retentionWindows,
        ICollection<string> errors)
    {
        if (retentionWindows is null || retentionWindows.Count == 0)
        {
            errors.Add("retentionWindows must include every retention class.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var retentionWindow in retentionWindows)
        {
            if (!RequireSupported(
                    retentionWindow.RetentionClass,
                    MemoryRetentionClass.All,
                    "retentionWindows.retentionClass",
                    errors))
            {
                continue;
            }

            if (!seen.Add(retentionWindow.RetentionClass))
            {
                errors.Add($"retentionWindows contains duplicate class '{retentionWindow.RetentionClass}'.");
            }

            ValidateRetentionWindow(retentionWindow, errors);
        }

        foreach (var requiredRetentionClass in MemoryRetentionClass.All)
        {
            if (!seen.Contains(requiredRetentionClass))
            {
                errors.Add($"retentionWindows must include '{requiredRetentionClass}'.");
            }
        }
    }

    private static void ValidateRetentionWindow(
        GovernanceRetentionWindow retentionWindow,
        ICollection<string> errors)
    {
        if (retentionWindow.MetadataRetentionDays <= 0)
        {
            errors.Add($"retentionWindows.{retentionWindow.RetentionClass}.metadataRetentionDays must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(retentionWindow.ActionOnExpiry))
        {
            errors.Add($"retentionWindows.{retentionWindow.RetentionClass}.actionOnExpiry is required.");
        }

        var stricterSensitivityClasses = ValidateNonEmptyValuesOrEmpty(
            retentionWindow.StricterSensitivityClasses,
            $"retentionWindows.{retentionWindow.RetentionClass}.stricterSensitivityClasses",
            errors);
        foreach (var sensitivity in stricterSensitivityClasses)
        {
            if (!MemorySensitivity.All.Contains(sensitivity))
            {
                errors.Add(
                    $"retentionWindows.{retentionWindow.RetentionClass}.stricterSensitivityClasses contains unsupported value '{sensitivity}'.");
            }
        }

        if (retentionWindow.RetentionClass == MemoryRetentionClass.LegalHold)
        {
            if (retentionWindow.PayloadRetentionDays is not null)
            {
                errors.Add("retentionWindows.legal_hold.payloadRetentionDays must be null.");
            }

            return;
        }

        if (retentionWindow.RetentionClass == MemoryRetentionClass.ErasureRequested)
        {
            if (retentionWindow.PayloadRetentionDays != 0)
            {
                errors.Add("retentionWindows.erasure_requested.payloadRetentionDays must be zero.");
            }

            return;
        }

        if (retentionWindow.PayloadRetentionDays is null or <= 0)
        {
            errors.Add($"retentionWindows.{retentionWindow.RetentionClass}.payloadRetentionDays must be greater than zero.");
            return;
        }

        if (retentionWindow.MetadataRetentionDays < retentionWindow.PayloadRetentionDays)
        {
            errors.Add(
                $"retentionWindows.{retentionWindow.RetentionClass}.metadataRetentionDays must be at least payloadRetentionDays.");
        }
    }

    private static void ValidateExternalPayloadPolicy(
        GovernanceExternalPayloadPolicy? externalPayloadPolicy,
        IReadOnlySet<string> evidenceLocationIds,
        ICollection<string> errors)
    {
        if (externalPayloadPolicy is null)
        {
            errors.Add("externalPayloadPolicy is required.");
            return;
        }

        if (!RequireSupported(externalPayloadPolicy.Mode, GovernanceExternalPayloadModes.All, "externalPayloadPolicy.mode", errors))
        {
            return;
        }

        if (!evidenceLocationIds.Contains(externalPayloadPolicy.EvidenceLocationId))
        {
            errors.Add("externalPayloadPolicy.evidenceLocationId must reference an evidence location.");
        }

        var storeKinds = ValidateNonEmptyValuesOrEmpty(
            externalPayloadPolicy.AllowedStoreKinds,
            "externalPayloadPolicy.allowedStoreKinds",
            errors);

        if (externalPayloadPolicy.Mode == GovernanceExternalPayloadModes.Disabled)
        {
            if (storeKinds.Count != 0)
            {
                errors.Add("externalPayloadPolicy.allowedStoreKinds must be empty when mode is disabled.");
            }

            return;
        }

        if (storeKinds.Count == 0)
        {
            errors.Add("externalPayloadPolicy.allowedStoreKinds is required when mode is audit_only or allowed.");
        }

        if (externalPayloadPolicy.Mode == GovernanceExternalPayloadModes.Allowed
            && !externalPayloadPolicy.DeletionEvidenceRequired)
        {
            errors.Add("externalPayloadPolicy.deletionEvidenceRequired must be true when mode is allowed.");
        }
    }

    private static void ValidateExceptionRecords(
        IReadOnlyList<GovernanceExceptionRecord>? exceptionRecords,
        IReadOnlySet<string> evidenceLocationIds,
        ICollection<string> errors,
        DateOnly today)
    {
        if (exceptionRecords is null)
        {
            errors.Add("exceptionRecords must be present, even when empty.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exceptionRecord in exceptionRecords)
        {
            if (string.IsNullOrWhiteSpace(exceptionRecord.Id))
            {
                errors.Add("exceptionRecords.id is required.");
            }
            else if (!seen.Add(exceptionRecord.Id))
            {
                errors.Add($"exceptionRecords contains duplicate id '{exceptionRecord.Id}'.");
            }

            _ = ValidateSupportedValues(
                exceptionRecord.Areas,
                GovernanceResidencyAreas.All,
                $"exceptionRecords.{exceptionRecord.Id}.areas",
                errors);

            if (string.IsNullOrWhiteSpace(exceptionRecord.Scope))
            {
                errors.Add($"exceptionRecords.{exceptionRecord.Id}.scope is required.");
            }

            if (string.IsNullOrWhiteSpace(exceptionRecord.ApprovedBy))
            {
                errors.Add($"exceptionRecords.{exceptionRecord.Id}.approvedBy is required.");
            }

            if (exceptionRecord.ExpiresOn <= today)
            {
                errors.Add($"exceptionRecords.{exceptionRecord.Id}.expiresOn must be in the future.");
            }

            if (!evidenceLocationIds.Contains(exceptionRecord.EvidenceLocationId))
            {
                errors.Add($"exceptionRecords.{exceptionRecord.Id}.evidenceLocationId must reference an evidence location.");
            }

            if (string.IsNullOrWhiteSpace(exceptionRecord.Reason))
            {
                errors.Add($"exceptionRecords.{exceptionRecord.Id}.reason is required.");
            }
        }
    }

    private static IReadOnlySet<string> ValidateEvidenceLocations(
        string environment,
        IReadOnlyList<GovernanceEvidenceLocation>? evidenceLocations,
        ICollection<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        if (evidenceLocations is null || evidenceLocations.Count == 0)
        {
            errors.Add("evidenceLocations must include release, governance, backup/restore, and audit-export locations.");
            return ids;
        }

        foreach (var evidenceLocation in evidenceLocations)
        {
            if (string.IsNullOrWhiteSpace(evidenceLocation.Id))
            {
                errors.Add("evidenceLocations.id is required.");
            }
            else if (!ids.Add(evidenceLocation.Id))
            {
                errors.Add($"evidenceLocations contains duplicate id '{evidenceLocation.Id}'.");
            }

            RequireSupported(evidenceLocation.Kind, GovernanceEvidenceLocationKinds.All, "evidenceLocations.kind", errors);

            if (string.IsNullOrWhiteSpace(evidenceLocation.Uri))
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.uri is required.");
            }

            if (ContainsSecretLikeValue(evidenceLocation.Uri))
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.uri must not contain secret-like values.");
            }

            if (string.IsNullOrWhiteSpace(evidenceLocation.Region))
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.region is required.");
            }

            if (evidenceLocation.RetentionDays <= 0)
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.retentionDays must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(evidenceLocation.AccessOwner))
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.accessOwner is required.");
            }

            if (!evidenceLocation.PayloadSafeOnly)
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.payloadSafeOnly must be true.");
            }

            if ((environment == EnvironmentGovernancePolicyEnvironments.Pilot
                    || environment == EnvironmentGovernancePolicyEnvironments.Production)
                && evidenceLocation.Kind == GovernanceEvidenceLocationKinds.LocalArtifact)
            {
                errors.Add($"evidenceLocations.{evidenceLocation.Id}.kind must not be local_artifact for pilot or production.");
            }
        }

        foreach (var requiredLocationId in GovernanceEvidenceLocationIds.Required)
        {
            if (!ids.Contains(requiredLocationId))
            {
                errors.Add($"evidenceLocations must include '{requiredLocationId}'.");
            }
        }

        return ids;
    }

    private static bool RequireSupported(
        string? value,
        IReadOnlySet<string> supportedValues,
        string fieldName,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return false;
        }

        if (!supportedValues.Contains(value))
        {
            errors.Add($"{fieldName} must be one of: {string.Join(", ", supportedValues)}.");
            return false;
        }

        return true;
    }

    private static IReadOnlySet<string> ValidateSupportedValues(
        IReadOnlyList<string>? values,
        IReadOnlySet<string> supportedValues,
        string fieldName,
        ICollection<string> errors)
    {
        var normalizedValues = ValidateNonEmptyValues(values, fieldName, errors);
        foreach (var value in normalizedValues)
        {
            if (!supportedValues.Contains(value))
            {
                errors.Add($"{fieldName} contains unsupported value '{value}'.");
            }
        }

        return normalizedValues;
    }

    private static IReadOnlySet<string> ValidateNonEmptyValues(
        IReadOnlyList<string>? values,
        string fieldName,
        ICollection<string> errors)
    {
        var normalizedValues = ValidateNonEmptyValuesOrEmpty(values, fieldName, errors);

        if (normalizedValues.Count == 0)
        {
            errors.Add($"{fieldName} must contain at least one value.");
        }

        return normalizedValues;
    }

    private static IReadOnlySet<string> ValidateNonEmptyValuesOrEmpty(
        IReadOnlyList<string>? values,
        string fieldName,
        ICollection<string> errors)
    {
        var normalizedValues = new HashSet<string>(StringComparer.Ordinal);
        if (values is null)
        {
            errors.Add($"{fieldName} is required.");
            return normalizedValues;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{fieldName} contains an empty value.");
            }
            else if (!normalizedValues.Add(value))
            {
                errors.Add($"{fieldName} contains duplicate value '{value}'.");
            }
        }

        return normalizedValues;
    }

    private static bool ContainsSecretLikeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token=", StringComparison.OrdinalIgnoreCase);
    }
}
