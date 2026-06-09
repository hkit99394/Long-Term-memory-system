namespace MemorySystem.Api.Admin;

public sealed record AdminOrganizationListResponse(
    string ContractId,
    IReadOnlyList<AdminOrganizationSummaryResponse> Organizations,
    int ReturnedCount,
    int Limit,
    string? NextCursor,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminOrganizationDetailResponse(
    string ContractId,
    AdminOrganizationSummaryResponse Organization,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectListResponse(
    string ContractId,
    IReadOnlyList<AdminProjectSummaryResponse> Projects,
    int ReturnedCount,
    int Limit,
    string? NextCursor,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectDetailResponse(
    string ContractId,
    AdminProjectSummaryResponse Project,
    AdminProjectRegistrationEvidenceResponse? LatestRegistrationEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminOrganizationSummaryResponse(
    Guid OrganizationId,
    string OrganizationName,
    string ActorAccessLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AdminProjectStatusCountsResponse ProjectStatusCounts,
    int ProjectCount,
    int OrganizationMembershipCount,
    int ProjectMembershipCount,
    int RoleAssignmentCount,
    int NamespaceGrantCount);

public sealed record AdminProjectSummaryResponse(
    Guid ProjectId,
    Guid OrganizationId,
    string OrganizationName,
    string ProjectName,
    string ProjectStatus,
    string ActorAccessLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ProjectMembershipCount,
    int RoleDefinitionCount,
    int ActiveRoleDefinitionCount,
    int RoleAssignmentCount,
    int NamespaceGrantCount);

public sealed record AdminProjectStatusCountsResponse(
    int Planned,
    int Active,
    int Archived,
    int Deleted);

public sealed record AdminProjectRegistrationEvidenceResponse(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    Guid? IdempotencyRecordId,
    string? RegistrationRequestHash,
    string? AccessPreviewReportId,
    string? AuditExportId,
    int SourceDocumentCount,
    int SourceHashCoveragePercent,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectScopeSettingsDetailResponse(
    string ContractId,
    AdminProjectManagementContextResponse Project,
    AdminProjectScopeSettingsResponse ScopeSettings,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectLifecycleUpdateResponse(
    string ContractId,
    string Status,
    AdminProjectManagementContextResponse Project,
    string PreviousProjectStatus,
    AdminProjectManagementAuditEvidenceResponse AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectScopeSettingsUpdateResponse(
    string ContractId,
    string Status,
    AdminProjectManagementContextResponse Project,
    AdminProjectScopeSettingsResponse ScopeSettings,
    AdminProjectManagementAuditEvidenceResponse AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectManagementContextResponse(
    Guid ProjectId,
    Guid OrganizationId,
    string ProjectName,
    string ProjectStatus);

public sealed record AdminProjectScopeSettingsResponse(
    Guid ProjectId,
    string DefaultNamespacePrefix,
    bool SourceHashRequired,
    string MemoryRetentionClass,
    int ReviewCadenceDays,
    Guid? UpdatedByPrincipalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDefault);

public sealed record AdminProjectManagementAuditEvidenceResponse(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    string ActionType,
    string ResourceType,
    string ResourceId,
    string AuditEvidenceId);

public sealed record AdminManagementActivityResponse(
    string ContractId,
    AdminManagementActivityScopeResponse Scope,
    IReadOnlyList<AdminManagementActivityEntryResponse> Entries,
    int ReturnedCount,
    int Limit,
    string? NextCursor,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminManagementActivityScopeResponse(
    string ScopeType,
    Guid ScopeId,
    Guid OrganizationId,
    string OrganizationName,
    Guid? ProjectId,
    string? ProjectName,
    string? ProjectStatus);

public sealed record AdminManagementActivityEntryResponse(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    Guid? ActorPrincipalId,
    Guid? TargetPrincipalId,
    string ActionType,
    string Outcome,
    string? ScopeType,
    string? ScopeId,
    string? RoleId,
    string? NamespacePrefix,
    string? Permission,
    string? ResourceType,
    string? ResourceId,
    string? RequestMethod,
    string? RequestPath,
    string? CorrelationId,
    string? Operation,
    string? SourceContractId,
    string? AuditEvidenceId,
    string Summary,
    IReadOnlyList<AdminManagementActivityMetadataResponse> Metadata);

public sealed record AdminManagementActivityMetadataResponse(
    string Key,
    string Value);

public sealed record AdminProjectRoleDefinitionListResponse(
    string ContractId,
    AdminProjectManagementContextResponse Project,
    IReadOnlyList<AdminProjectRoleDefinitionManagementResponse> Roles,
    int ActiveCount,
    int DisabledCount,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectRoleDefinitionUpdateResponse(
    string ContractId,
    string Status,
    AdminProjectManagementContextResponse Project,
    AdminProjectRoleDefinitionManagementResponse Role,
    AdminProjectManagementAuditEvidenceResponse AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminProjectRoleDefinitionManagementResponse(
    Guid ProjectId,
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    int AssignmentCount,
    int RoleGrantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminAccessInventoryResponse(
    string ContractId,
    AdminAccessInventoryScopeResponse Scope,
    AdminAccessInventoryCountsResponse Counts,
    IReadOnlyList<AdminOrganizationMembershipInventoryResponse> OrganizationMemberships,
    IReadOnlyList<AdminProjectMembershipInventoryResponse> ProjectMemberships,
    IReadOnlyList<AdminRoleAssignmentInventoryResponse> RoleAssignments,
    IReadOnlyList<AdminNamespaceGrantInventoryResponse> NamespaceGrants,
    IReadOnlyList<AdminStaleAccessPromptResponse> StaleAccessPrompts,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminAccessRevocationResponse(
    string ContractId,
    string Status,
    AdminAccessInventoryScopeResponse Scope,
    AdminRevokedAccessResponse RevokedAccess,
    AdminProjectManagementAuditEvidenceResponse AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminAccessInventoryScopeResponse(
    string ScopeType,
    Guid ScopeId,
    Guid OrganizationId,
    string OrganizationName,
    Guid? ProjectId,
    string? ProjectName,
    string? ProjectStatus);

public sealed record AdminAccessInventoryCountsResponse(
    int OrganizationMemberships,
    int ProjectMemberships,
    int RoleAssignments,
    int NamespaceGrants,
    int StaleAccessPrompts);

public sealed record AdminOrganizationMembershipInventoryResponse(
    string AccessRecordType,
    string AccessRecordId,
    Guid OrganizationId,
    string OrganizationName,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string PrincipalStatus,
    string AccessLevel,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminProjectMembershipInventoryResponse(
    string AccessRecordType,
    string AccessRecordId,
    Guid ProjectId,
    string ProjectName,
    string ProjectStatus,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string PrincipalStatus,
    string AccessLevel,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminRoleAssignmentInventoryResponse(
    string AccessRecordType,
    Guid AccessRecordId,
    string ScopeType,
    Guid ScopeId,
    string ScopeName,
    string? ProjectStatus,
    Guid PrincipalId,
    string PrincipalDisplayName,
    string PrincipalStatus,
    string RoleId,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminNamespaceGrantInventoryResponse(
    string AccessRecordType,
    Guid AccessRecordId,
    string ScopeType,
    Guid ScopeId,
    string ScopeName,
    string? ProjectStatus,
    Guid? PrincipalId,
    string? PrincipalDisplayName,
    string? PrincipalStatus,
    string? RoleId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt,
    string? ReviewPrompt);

public sealed record AdminStaleAccessPromptResponse(
    string AccessRecordType,
    string AccessRecordId,
    string Severity,
    string Prompt);

public sealed record AdminRevokedAccessResponse(
    string AccessRecordType,
    string AccessRecordId,
    Guid? PrincipalId,
    string? PrincipalDisplayName,
    string? RoleId,
    string? NamespacePrefix,
    string? Permission,
    string Reason,
    string AuditEvidenceId);

public sealed record AdminGrantMatrixResponse(
    string ContractId,
    AdminProjectManagementContextResponse Project,
    IReadOnlyList<AdminGrantMatrixPresetResponse> Presets,
    IReadOnlyList<AdminGrantMatrixRoleResponse> Roles,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminGrantMatrixUpdateResponse(
    string ContractId,
    string Status,
    AdminProjectManagementContextResponse Project,
    AdminGrantMatrixRoleResponse Role,
    AdminProjectManagementAuditEvidenceResponse AuditEvidence,
    bool PayloadSafe,
    bool RawSourcePayloadsIncluded);

public sealed record AdminGrantMatrixPresetResponse(
    string PresetId,
    string DisplayName,
    string Description,
    IReadOnlyList<AdminGrantMatrixPresetGrantResponse> Grants);

public sealed record AdminGrantMatrixPresetGrantResponse(
    string NamespaceTemplate,
    string Permission);

public sealed record AdminGrantMatrixRoleResponse(
    string RoleId,
    string DisplayName,
    string? Description,
    string? TemplateRoleId,
    string Status,
    string RecommendedPresetId,
    string PresetAlignment,
    IReadOnlyList<AdminGrantMatrixGrantResponse> Grants,
    IReadOnlyList<AdminGrantMatrixEffectivePreviewResponse> EffectiveAccessPreviews);

public sealed record AdminGrantMatrixGrantResponse(
    Guid? GrantId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset? CreatedAt,
    bool FromPreset);

public sealed record AdminGrantMatrixEffectivePreviewResponse(
    Guid PrincipalId,
    string RoleId,
    string Permission,
    string NamespacePrefix,
    bool Allowed,
    string Reason,
    string EvaluatedBy);
