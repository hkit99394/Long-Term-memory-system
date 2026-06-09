type AdminManagementItemType = "organization" | "project";
type AdminManagementBenchmarkStatus = "done" | "blocked" | "needs_review";
type AdminManagementAccessDriftStatus = "pending" | "clear" | "drift_found";
type AdminManagementSqlFallbackStatus = "none" | "used";

interface AdminManagementRevocationTarget {
  accessRecordType: string;
  accessRecordId: string | null;
  principalId: string | null;
  scopeType: AdminManagementAccessInventoryScope["scopeType"];
  scopeId: string;
  title: string;
  meta: string;
  reviewPrompt: string | null;
}

interface AdminManagementSelection {
  type: AdminManagementItemType;
  id: string;
}

interface AdminManagementViewState {
  organizations: AdminManagementOrganizationSummary[];
  projects: AdminManagementProjectSummary[];
  selectedItem: AdminManagementSelection | null;
  organizationDetail: AdminManagementOrganizationDetailResponse | null;
  projectDetail: AdminManagementProjectDetailResponse | null;
  scopeSettingsDetail: AdminManagementScopeSettingsDetailResponse | null;
  accessInventoryDetail: AdminManagementAccessInventoryResponse | null;
  roleDefinitionsDetail: AdminManagementProjectRoleDefinitionListResponse | null;
  grantMatrixDetail: AdminManagementGrantMatrixResponse | null;
  managementActivityDetail: AdminManagementActivityResponse | null;
  evidencePayload: unknown | null;
  loading: boolean;
  detailLoading: boolean;
  error: string | null;
  detailError: string | null;
  benchmarkStartedAt: string;
  benchmarkFirstDetailLoadedAt: string;
  benchmarkLastModificationCompletedAt: string;
  benchmarkLatestOperation: string;
  benchmarkOperationFailureCount: number;
  benchmarkAccessDriftStatus: AdminManagementAccessDriftStatus;
  benchmarkSqlFallbackStatus: AdminManagementSqlFallbackStatus;
  benchmarkUserConfidenceScore: string;
}

interface AdminManagementBenchmarkItem {
  id: string;
  label: string;
  status: AdminManagementBenchmarkStatus;
  detail: string;
}

interface AdminManagementOrganizationListResponse {
  contractId: string;
  organizations: AdminManagementOrganizationSummary[];
  returnedCount: number;
  limit: number;
  nextCursor: string | null;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementOrganizationDetailResponse {
  contractId: string;
  organization: AdminManagementOrganizationSummary;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementProjectListResponse {
  contractId: string;
  projects: AdminManagementProjectSummary[];
  returnedCount: number;
  limit: number;
  nextCursor: string | null;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementProjectDetailResponse {
  contractId: string;
  project: AdminManagementProjectSummary;
  latestRegistrationEvidence: AdminManagementRegistrationEvidence | null;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementScopeSettingsDetailResponse {
  contractId: string;
  project: AdminManagementProjectContext;
  scopeSettings: AdminManagementScopeSettings;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementLifecycleUpdateResponse {
  contractId: string;
  status: string;
  project: AdminManagementProjectContext;
  previousProjectStatus: string;
  auditEvidence: AdminManagementAuditEvidence;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementScopeSettingsUpdateResponse {
  contractId: string;
  status: string;
  project: AdminManagementProjectContext;
  scopeSettings: AdminManagementScopeSettings;
  auditEvidence: AdminManagementAuditEvidence;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementAccessInventoryResponse {
  contractId: string;
  scope: AdminManagementAccessInventoryScope;
  counts: AdminManagementAccessInventoryCounts;
  organizationMemberships: AdminManagementOrganizationMembershipInventory[];
  projectMemberships: AdminManagementProjectMembershipInventory[];
  roleAssignments: AdminManagementRoleAssignmentInventory[];
  namespaceGrants: AdminManagementNamespaceGrantInventory[];
  staleAccessPrompts: AdminManagementStaleAccessPrompt[];
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementAccessRevocationResponse {
  contractId: string;
  status: string;
  scope: AdminManagementAccessInventoryScope;
  revokedAccess: AdminManagementRevokedAccess;
  auditEvidence: AdminManagementAuditEvidence;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementGrantMatrixResponse {
  contractId: string;
  project: AdminManagementProjectContext;
  presets: AdminManagementGrantMatrixPreset[];
  roles: AdminManagementGrantMatrixRole[];
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementProjectRoleDefinitionListResponse {
  contractId: string;
  project: AdminManagementProjectContext;
  roles: AdminManagementProjectRoleDefinition[];
  activeCount: number;
  disabledCount: number;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementProjectRoleDefinitionUpdateResponse {
  contractId: string;
  status: string;
  project: AdminManagementProjectContext;
  role: AdminManagementProjectRoleDefinition;
  auditEvidence: AdminManagementAuditEvidence;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementProjectRoleDefinition {
  projectId: string;
  roleId: string;
  displayName: string;
  description: string | null;
  templateRoleId: string | null;
  status: string;
  assignmentCount: number;
  roleGrantCount: number;
  createdAt: string;
  updatedAt: string;
}

interface AdminManagementGrantMatrixUpdateResponse {
  contractId: string;
  status: string;
  project: AdminManagementProjectContext;
  role: AdminManagementGrantMatrixRole;
  auditEvidence: AdminManagementAuditEvidence;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementActivityResponse {
  contractId: string;
  scope: AdminManagementActivityScope;
  entries: AdminManagementActivityEntry[];
  returnedCount: number;
  limit: number;
  nextCursor: string | null;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementActivityScope {
  scopeType: "org" | "project";
  scopeId: string;
  organizationId: string;
  organizationName: string;
  projectId: string | null;
  projectName: string | null;
  projectStatus: string | null;
}

interface AdminManagementActivityEntry {
  auditEventId: string;
  occurredAt: string;
  actorPrincipalId: string | null;
  targetPrincipalId: string | null;
  actionType: string;
  outcome: string;
  scopeType: string | null;
  scopeId: string | null;
  roleId: string | null;
  namespacePrefix: string | null;
  permission: string | null;
  resourceType: string | null;
  resourceId: string | null;
  requestMethod: string | null;
  requestPath: string | null;
  correlationId: string | null;
  operation: string | null;
  sourceContractId: string | null;
  auditEvidenceId: string | null;
  summary: string;
  metadata: AdminManagementActivityMetadata[];
}

interface AdminManagementActivityMetadata {
  key: string;
  value: string;
}

interface AdminManagementGrantMatrixPreset {
  presetId: string;
  displayName: string;
  description: string;
  grants: AdminManagementGrantMatrixPresetGrant[];
}

interface AdminManagementGrantMatrixPresetGrant {
  namespaceTemplate: string;
  permission: string;
}

interface AdminManagementGrantMatrixRole {
  roleId: string;
  displayName: string;
  description: string | null;
  templateRoleId: string | null;
  status: string;
  recommendedPresetId: string;
  presetAlignment: string;
  grants: AdminManagementGrantMatrixGrant[];
  effectiveAccessPreviews: AdminManagementGrantMatrixEffectivePreview[];
}

interface AdminManagementGrantMatrixGrant {
  grantId: string | null;
  namespacePrefix: string;
  permission: string;
  createdAt: string | null;
  fromPreset: boolean;
}

interface AdminManagementGrantMatrixEffectivePreview {
  principalId: string;
  roleId: string;
  permission: string;
  namespacePrefix: string;
  allowed: boolean;
  reason: string;
  evaluatedBy: string;
}

interface AdminManagementOrganizationSummary {
  organizationId: string;
  organizationName: string;
  actorAccessLevel: string;
  createdAt: string;
  updatedAt: string;
  projectStatusCounts: AdminManagementProjectStatusCounts;
  projectCount: number;
  organizationMembershipCount: number;
  projectMembershipCount: number;
  roleAssignmentCount: number;
  namespaceGrantCount: number;
}

interface AdminManagementProjectSummary {
  projectId: string;
  organizationId: string;
  organizationName: string;
  projectName: string;
  projectStatus: string;
  actorAccessLevel: string;
  createdAt: string;
  updatedAt: string;
  projectMembershipCount: number;
  roleDefinitionCount: number;
  activeRoleDefinitionCount: number;
  roleAssignmentCount: number;
  namespaceGrantCount: number;
}

interface AdminManagementProjectStatusCounts {
  planned: number;
  active: number;
  archived: number;
  deleted: number;
}

interface AdminManagementRegistrationEvidence {
  auditEventId: string;
  occurredAt: string;
  idempotencyRecordId: string | null;
  registrationRequestHash: string | null;
  accessPreviewReportId: string | null;
  auditExportId: string | null;
  sourceDocumentCount: number;
  sourceHashCoveragePercent: number;
  payloadSafe: boolean;
  rawSourcePayloadsIncluded: boolean;
}

interface AdminManagementProjectContext {
  projectId: string;
  organizationId: string;
  projectName: string;
  projectStatus: string;
}

interface AdminManagementScopeSettings {
  projectId: string;
  defaultNamespacePrefix: string;
  sourceHashRequired: boolean;
  memoryRetentionClass: string;
  reviewCadenceDays: number;
  updatedByPrincipalId: string | null;
  createdAt: string;
  updatedAt: string;
  isDefault: boolean;
}

interface AdminManagementAuditEvidence {
  auditEventId: string;
  occurredAt: string;
  actionType: string;
  resourceType: string;
  resourceId: string;
  auditEvidenceId: string;
}

interface AdminManagementAccessInventoryScope {
  scopeType: "org" | "project";
  scopeId: string;
  organizationId: string;
  organizationName: string;
  projectId: string | null;
  projectName: string | null;
  projectStatus: string | null;
}

interface AdminManagementAccessInventoryCounts {
  organizationMemberships: number;
  projectMemberships: number;
  roleAssignments: number;
  namespaceGrants: number;
  staleAccessPrompts: number;
}

interface AdminManagementOrganizationMembershipInventory {
  accessRecordType: "organization_membership";
  accessRecordId: string;
  organizationId: string;
  organizationName: string;
  principalId: string;
  principalDisplayName: string;
  principalStatus: string;
  accessLevel: string;
  createdAt: string;
  reviewPrompt: string | null;
}

interface AdminManagementProjectMembershipInventory {
  accessRecordType: "project_membership";
  accessRecordId: string;
  projectId: string;
  projectName: string;
  projectStatus: string;
  principalId: string;
  principalDisplayName: string;
  principalStatus: string;
  accessLevel: string;
  createdAt: string;
  reviewPrompt: string | null;
}

interface AdminManagementRoleAssignmentInventory {
  accessRecordType: "role_assignment";
  accessRecordId: string;
  scopeType: "org" | "project";
  scopeId: string;
  scopeName: string;
  projectStatus: string | null;
  principalId: string;
  principalDisplayName: string;
  principalStatus: string;
  roleId: string;
  createdAt: string;
  reviewPrompt: string | null;
}

interface AdminManagementNamespaceGrantInventory {
  accessRecordType: "namespace_grant";
  accessRecordId: string;
  scopeType: "org" | "project";
  scopeId: string;
  scopeName: string;
  projectStatus: string | null;
  principalId: string | null;
  principalDisplayName: string | null;
  principalStatus: string | null;
  roleId: string | null;
  namespacePrefix: string;
  permission: string;
  createdAt: string;
  reviewPrompt: string | null;
}

interface AdminManagementStaleAccessPrompt {
  accessRecordType: string;
  accessRecordId: string;
  severity: string;
  prompt: string;
}

interface AdminManagementRevokedAccess {
  accessRecordType: string;
  accessRecordId: string;
  principalId: string | null;
  principalDisplayName: string | null;
  roleId: string | null;
  namespacePrefix: string | null;
  permission: string | null;
  reason: string;
  auditEvidenceId: string;
}

function createManagementViewState(): AdminManagementViewState {
  return {
    organizations: [],
    projects: [],
    selectedItem: null,
    organizationDetail: null,
    projectDetail: null,
    scopeSettingsDetail: null,
    accessInventoryDetail: null,
    roleDefinitionsDetail: null,
    grantMatrixDetail: null,
    managementActivityDetail: null,
    evidencePayload: null,
    loading: false,
    detailLoading: false,
    error: null,
    detailError: null,
    benchmarkStartedAt: "",
    benchmarkFirstDetailLoadedAt: "",
    benchmarkLastModificationCompletedAt: "",
    benchmarkLatestOperation: "",
    benchmarkOperationFailureCount: 0,
    benchmarkAccessDriftStatus: "pending",
    benchmarkSqlFallbackStatus: "none",
    benchmarkUserConfidenceScore: ""
  };
}

async function loadManagement(): Promise<void> {
  setBusy(true);
  setStatus("Loading");
  ensureManagementBenchmarkStarted();
  state.selectedSource = null;
  state.management.loading = true;
  state.management.error = null;
  state.management.detailError = null;
  render();

  try {
    await refreshManagementDirectory();
    await loadSelectedManagementDetail(false);

    setStatus(`${state.management.organizations.length} orgs · ${state.management.projects.length} projects`);
  } catch (error) {
    state.management.organizations = [];
    state.management.projects = [];
    state.management.selectedItem = null;
    state.management.organizationDetail = null;
    state.management.projectDetail = null;
    state.management.scopeSettingsDetail = null;
    state.management.accessInventoryDetail = null;
    state.management.roleDefinitionsDetail = null;
    state.management.grantMatrixDetail = null;
    state.management.managementActivityDetail = null;
    state.management.evidencePayload = null;
    state.management.error = errorMessage(error);
    setStatus("Error");
  } finally {
    state.management.loading = false;
    setBusy(false);
    render();
  }
}

async function refreshManagementDirectory(): Promise<void> {
  const query = elements.query.value.trim();
  const organizationParams = new URLSearchParams({ limit: "50" });
  const projectParams = new URLSearchParams({ limit: "50" });

  if (query) {
    organizationParams.set("q", query);
    projectParams.set("q", query);
  }

  const projectStatus = elements.projectStatusFilter.value;
  if (projectStatus !== "all") {
    projectParams.set("status", projectStatus);
  }

  const [organizationResponse, projectResponse] = await Promise.all([
    apiFetch<AdminManagementOrganizationListResponse>(`/api/admin/organizations?${organizationParams}`),
    apiFetch<AdminManagementProjectListResponse>(`/api/admin/projects?${projectParams}`)
  ]);

  state.management.organizations = organizationResponse.organizations;
  state.management.projects = projectResponse.projects;
  ensureManagementSelection();
}

async function loadSelectedManagementDetail(showBusy: boolean): Promise<void> {
  const selected = state.management.selectedItem;
  state.management.organizationDetail = null;
  state.management.projectDetail = null;
  state.management.scopeSettingsDetail = null;
  state.management.accessInventoryDetail = null;
  state.management.roleDefinitionsDetail = null;
  state.management.grantMatrixDetail = null;
  state.management.managementActivityDetail = null;
  state.management.evidencePayload = null;
  state.management.detailError = null;

  if (!selected) {
    return;
  }

  if (showBusy) {
    state.management.detailLoading = true;
    setBusy(true);
    setStatus("Loading detail");
    render();
  }

  try {
    if (selected.type === "organization") {
      const [organizationDetail, accessInventory, managementActivity] = await Promise.all([
        apiFetch<AdminManagementOrganizationDetailResponse>(`/api/admin/organizations/${selected.id}`),
        apiFetch<AdminManagementAccessInventoryResponse>(`/api/admin/organizations/${selected.id}/access-inventory`),
        apiFetch<AdminManagementActivityResponse>(`/api/admin/organizations/${selected.id}/management-activity?limit=20`)
      ]);
      state.management.organizationDetail = organizationDetail;
      state.management.accessInventoryDetail = accessInventory;
      state.management.managementActivityDetail = managementActivity;
      state.management.evidencePayload = managementActivity;
    } else {
      const projectDetail = await apiFetch<AdminManagementProjectDetailResponse>(`/api/admin/projects/${selected.id}`);
      const [scopeSettings, accessInventory, roleDefinitions, grantMatrix, managementActivity] = await Promise.all([
        loadOptionalManagementDetail(
          apiFetch<AdminManagementScopeSettingsDetailResponse>(`/api/admin/projects/${selected.id}/scope-settings`)),
        loadOptionalManagementDetail(
          apiFetch<AdminManagementAccessInventoryResponse>(`/api/admin/projects/${selected.id}/access-inventory`)),
        loadOptionalManagementDetail(
          apiFetch<AdminManagementProjectRoleDefinitionListResponse>(`/api/admin/projects/${selected.id}/role-definitions`)),
        loadOptionalManagementDetail(
          apiFetch<AdminManagementGrantMatrixResponse>(`/api/admin/projects/${selected.id}/grant-matrix`)),
        loadOptionalManagementDetail(
          apiFetch<AdminManagementActivityResponse>(`/api/admin/projects/${selected.id}/management-activity?limit=20`))
      ]);
      state.management.projectDetail = projectDetail;
      state.management.scopeSettingsDetail = scopeSettings;
      state.management.accessInventoryDetail = accessInventory;
      state.management.roleDefinitionsDetail = roleDefinitions;
      state.management.grantMatrixDetail = grantMatrix;
      state.management.managementActivityDetail = managementActivity;
      state.management.evidencePayload = managementActivity ?? roleDefinitions ?? grantMatrix ?? accessInventory ?? scopeSettings ?? projectDetail;
    }

    markManagementDetailLoaded();
    setStatus("Management detail");
  } catch (error) {
    state.management.detailError = errorMessage(error);
    setStatus("Not visible");
  } finally {
    if (showBusy) {
      state.management.detailLoading = false;
      setBusy(false);
      render();
    }
  }
}

async function loadOptionalManagementDetail<T>(request: Promise<T>): Promise<T | null> {
  try {
    return await request;
  } catch {
    return null;
  }
}

async function refreshManagementActivityDetail(): Promise<void> {
  const selected = state.management.selectedItem;
  if (!selected) {
    state.management.managementActivityDetail = null;
    return;
  }

  state.management.managementActivityDetail = selected.type === "organization"
    ? await apiFetch<AdminManagementActivityResponse>(`/api/admin/organizations/${selected.id}/management-activity?limit=20`)
    : await apiFetch<AdminManagementActivityResponse>(`/api/admin/projects/${selected.id}/management-activity?limit=20`);
}

async function refreshManagementActivityDetailBestEffort(): Promise<void> {
  try {
    await refreshManagementActivityDetail();
  } catch {
    state.management.managementActivityDetail = null;
  }
}

function renderManagementList(): void {
  if (state.management.loading) {
    elements.resultList.append(emptyPanel("Loading organization/project management"));
    return;
  }

  if (state.management.error) {
    elements.resultList.append(emptyPanel(`Management failed to load: ${state.management.error}`));
    return;
  }

  if (state.management.organizations.length === 0 && state.management.projects.length === 0) {
    elements.resultList.append(emptyPanel("No visible organizations or projects"));
    return;
  }

  if (state.management.organizations.length > 0) {
    elements.resultList.append(managementSection(
      "Organizations",
      state.management.organizations.map(renderManagementOrganizationRow)));
  }

  if (state.management.projects.length > 0) {
    elements.resultList.append(managementSection(
      "Projects",
      state.management.projects.map(renderManagementProjectRow)));
  }
}

function renderManagementDetail(): void {
  if (state.management.loading || state.management.detailLoading) {
    elements.detail.append(emptyPanel("Loading management detail"));
    return;
  }

  if (state.management.error) {
    elements.detail.append(emptyPanel("Management list is unavailable"));
    return;
  }

  if (state.management.detailError) {
    elements.detail.append(emptyPanel(`Management detail is not visible to the caller: ${state.management.detailError}`));
    return;
  }

  if (state.management.organizationDetail) {
    renderManagementOrganizationDetail(state.management.organizationDetail);
    return;
  }

  if (state.management.projectDetail) {
    renderManagementProjectDetail(state.management.projectDetail);
    return;
  }

  elements.detail.append(emptyPanel("Select an organization or project"));
}

function renderManagementSourceDetail(): void {
  elements.sourceDetail.replaceChildren();

  if (state.management.loading) {
    elements.sourceDetail.append(emptyPanel("Loading management evidence"));
    return;
  }

  if (state.management.error) {
    elements.sourceDetail.append(emptyPanel(state.management.error));
    return;
  }

  const detail = state.management.evidencePayload
    ?? state.management.organizationDetail
    ?? state.management.projectDetail;
  if (!detail) {
    elements.sourceDetail.append(
      managementSuccessBenchmarkSection(),
      linkList(managementLinks()));
    return;
  }

  const benchmarkJson = document.createElement("pre");
  benchmarkJson.className = "source-json";
  benchmarkJson.textContent = JSON.stringify(managementSuccessBenchmarkEvidence(), null, 2);

  const json = document.createElement("pre");
  json.className = "source-json";
  json.textContent = JSON.stringify(detail, null, 2);
  elements.sourceDetail.append(
    managementSuccessBenchmarkSection(),
    heading("Payload-safe benchmark"),
    benchmarkJson,
    heading("Payload-safe detail"),
    json,
    linkList(managementLinks()));
}

function renderManagementOrganizationDetail(detail: AdminManagementOrganizationDetailResponse): void {
  const organization = detail.organization;
  const counts = organization.projectStatusCounts;
  elements.detail.append(
    heading(organization.organizationName),
    detailGrid([
      ["Organization", organization.organizationId],
      ["Access", organization.actorAccessLevel],
      ["Projects", organization.projectCount.toString()],
      ["Planned", counts.planned.toString()],
      ["Active", counts.active.toString()],
      ["Archived", counts.archived.toString()],
      ["Deleted", counts.deleted.toString()],
      ["Organization members", organization.organizationMembershipCount.toString()],
      ["Project members", organization.projectMembershipCount.toString()],
      ["Role assignments", organization.roleAssignmentCount.toString()],
      ["Namespace grants", organization.namespaceGrantCount.toString()],
      ["Payload safe", detail.payloadSafe ? "yes" : "no"],
      ["Raw source payloads", detail.rawSourcePayloadsIncluded ? "included" : "not included"],
      ["Updated", shortDate(organization.updatedAt)]
    ]),
    managementCountGrid([
      ["Planned", counts.planned],
      ["Active", counts.active],
      ["Archived", counts.archived],
      ["Deleted", counts.deleted]
    ]),
    managementAccessInventorySection(state.management.accessInventoryDetail),
    managementActivitySection(state.management.managementActivityDetail),
    actionRow(managementActions()));
}

function renderManagementProjectDetail(detail: AdminManagementProjectDetailResponse): void {
  const project = detail.project;
  const evidence = detail.latestRegistrationEvidence;
  const scopeSettings = state.management.scopeSettingsDetail?.scopeSettings;
  const rows: [string, string][] = [
    ["Project", project.projectId],
    ["Organization", `${project.organizationName} (${project.organizationId})`],
    ["Status", project.projectStatus],
    ["Access", project.actorAccessLevel],
    ["Project members", project.projectMembershipCount.toString()],
    ["Role definitions", project.roleDefinitionCount.toString()],
    ["Active roles", project.activeRoleDefinitionCount.toString()],
    ["Role assignments", project.roleAssignmentCount.toString()],
    ["Namespace grants", project.namespaceGrantCount.toString()],
    ["Payload safe", detail.payloadSafe ? "yes" : "no"],
    ["Raw source payloads", detail.rawSourcePayloadsIncluded ? "included" : "not included"],
    ["Updated", shortDate(project.updatedAt)]
  ];

  if (scopeSettings) {
    rows.push(
      ["Default namespace", scopeSettings.defaultNamespacePrefix],
      ["Source hash required", scopeSettings.sourceHashRequired ? "yes" : "no"],
      ["Retention", scopeSettings.memoryRetentionClass],
      ["Review cadence", `${scopeSettings.reviewCadenceDays} days`],
      ["Settings source", scopeSettings.isDefault ? "default" : "stored"]);
  }

  if (evidence) {
    rows.push(
      ["Registration evidence", evidence.auditEventId],
      ["Registered", shortDate(evidence.occurredAt)],
      ["Source documents", evidence.sourceDocumentCount.toString()],
      ["Source hash coverage", `${evidence.sourceHashCoveragePercent}%`],
      ["Access preview", evidence.accessPreviewReportId ?? ""],
      ["Audit export", evidence.auditExportId ?? ""]);
  }

  elements.detail.append(
    heading(project.projectName),
    pillRow([project.projectStatus, project.actorAccessLevel]),
    detailGrid(rows),
    managementAccessInventorySection(state.management.accessInventoryDetail),
    managementProjectRoleDefinitionsSection(state.management.roleDefinitionsDetail),
    managementGrantMatrixSection(state.management.grantMatrixDetail),
    managementActivitySection(state.management.managementActivityDetail),
    managementLifecycleForm(project),
    managementScopeSettingsForm(project, scopeSettings),
    actionRow(managementActions()));
}

function managementAccessInventorySection(inventory: AdminManagementAccessInventoryResponse | null): HTMLElement {
  const section = document.createElement("section");
  section.className = "management-access-inventory";
  section.append(heading("Access inventory"));

  if (!inventory) {
    section.append(emptyPanel("Access inventory is not loaded"));
    return section;
  }

  section.append(managementCountGrid([
    ["Org members", inventory.counts.organizationMemberships],
    ["Project members", inventory.counts.projectMemberships],
    ["Roles", inventory.counts.roleAssignments],
    ["Grants", inventory.counts.namespaceGrants],
    ["Review", inventory.counts.staleAccessPrompts]
  ]));

  if (inventory.staleAccessPrompts.length > 0) {
    const promptList = document.createElement("div");
    promptList.className = "management-access-prompts";
    for (const prompt of inventory.staleAccessPrompts) {
      promptList.append(line(prompt.prompt, "management-access-prompt"));
    }
    section.append(promptList);
  }

  section.append(
    managementAccessInventoryGroup(
      "Organization memberships",
      inventory.organizationMemberships.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: null,
        principalId: row.principalId,
        scopeType: "org",
        scopeId: row.organizationId,
        title: row.principalDisplayName,
        meta: `${row.accessLevel} · ${row.principalStatus} · ${row.organizationName}`,
        reviewPrompt: row.reviewPrompt
      }))),
    managementAccessInventoryGroup(
      "Project memberships",
      inventory.projectMemberships.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: null,
        principalId: row.principalId,
        scopeType: "project",
        scopeId: row.projectId,
        title: row.principalDisplayName,
        meta: `${row.accessLevel} · ${row.principalStatus} · ${row.projectName} · ${row.projectStatus}`,
        reviewPrompt: row.reviewPrompt
      }))),
    managementAccessInventoryGroup(
      "Role assignments",
      inventory.roleAssignments.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: row.accessRecordId,
        principalId: row.principalId,
        scopeType: row.scopeType,
        scopeId: row.scopeId,
        title: `${row.principalDisplayName} · ${row.roleId}`,
        meta: `${row.scopeName} · ${row.scopeType}${row.projectStatus ? ` · ${row.projectStatus}` : ""}`,
        reviewPrompt: row.reviewPrompt
      }))),
    managementAccessInventoryGroup(
      "Namespace grants",
      inventory.namespaceGrants.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: row.accessRecordId,
        principalId: row.principalId,
        scopeType: row.scopeType,
        scopeId: row.scopeId,
        title: row.principalDisplayName ?? `Role ${row.roleId ?? "target"}`,
        meta: `${row.permission} · ${row.namespacePrefix} · ${row.scopeName}`,
        reviewPrompt: row.reviewPrompt
      }))));

  return section;
}

function managementAccessInventoryGroup(
  title: string,
  rows: AdminManagementRevocationTarget[]): HTMLElement {
  const group = document.createElement("section");
  group.className = "management-access-group";
  group.append(line(title, "management-section-title"));

  if (rows.length === 0) {
    group.append(line("None", "memory-meta"));
    return group;
  }

  for (const row of rows) {
    group.append(managementAccessInventoryRecord(row));
  }

  return group;
}

function managementAccessInventoryRecord(
  row: AdminManagementRevocationTarget): HTMLElement {
  const card = document.createElement("div");
  card.className = "management-access-record";
  card.append(
    line(row.title, "management-access-title"),
    line(row.meta, "management-access-meta"));

  if (row.reviewPrompt) {
    card.append(line(row.reviewPrompt, "management-access-prompt"));
  }

  const form = document.createElement("form");
  form.className = "management-revoke-form";
  form.append(
    managementInputField("Reason", "reason", "Revocation reason", true),
    managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "secondary-action";
  button.textContent = "Revoke";
  button.disabled = state.busy;
  form.append(button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    void revokeManagementAccess(row, form);
  });

  card.append(form);
  return card;
}

function managementActivitySection(activity: AdminManagementActivityResponse | null): HTMLElement {
  const section = document.createElement("section");
  section.className = "management-activity";
  section.append(heading("Management activity"));

  if (!activity) {
    section.append(emptyPanel("Management activity is not loaded"));
    return section;
  }

  section.append(managementCountGrid([
    ["Entries", activity.returnedCount],
    ["Limit", activity.limit]
  ]));

  const list = document.createElement("div");
  list.className = "management-activity-list";
  if (activity.entries.length === 0) {
    list.append(line("No management activity recorded", "memory-meta"));
  } else {
    for (const entry of activity.entries) {
      list.append(managementActivityEntry(entry));
    }
  }

  section.append(list);
  return section;
}

function managementActivityEntry(entry: AdminManagementActivityEntry): HTMLElement {
  const card = document.createElement("section");
  card.className = "management-activity-entry";
  card.append(
    line(entry.summary, "management-access-title"),
    pillRow([entry.outcome, entry.sourceContractId ?? entry.actionType]),
    line(shortDate(entry.occurredAt), "management-access-meta"),
    line(managementActivityTargetText(entry), "management-access-meta"));

  const actorTarget = [
    entry.actorPrincipalId ? `actor ${entry.actorPrincipalId}` : null,
    entry.targetPrincipalId ? `target ${entry.targetPrincipalId}` : null,
    entry.roleId ? `role ${entry.roleId}` : null,
    entry.permission ? `permission ${entry.permission}` : null
  ].filter(Boolean).join(" · ");
  if (actorTarget) {
    card.append(line(actorTarget, "management-access-meta"));
  }

  if (entry.metadata.length > 0) {
    const metadata = document.createElement("div");
    metadata.className = "management-activity-meta";
    for (const item of entry.metadata) {
      metadata.append(line(`${item.key}: ${item.value}`, "management-activity-meta-row"));
    }

    card.append(metadata);
  }

  return card;
}

function managementActivityTargetText(entry: AdminManagementActivityEntry): string {
  const request = entry.requestMethod && entry.requestPath
    ? `${entry.requestMethod} ${entry.requestPath}`
    : null;
  const resource = entry.resourceType
    ? `${entry.resourceType}${entry.resourceId ? `:${entry.resourceId}` : ""}`
    : null;
  const scope = entry.scopeType && entry.scopeId
    ? `${entry.scopeType}:${entry.scopeId}`
    : null;

  return [request, resource, scope].filter(Boolean).join(" · ") || entry.auditEventId;
}

function managementProjectRoleDefinitionsSection(
  roleDefinitions: AdminManagementProjectRoleDefinitionListResponse | null): HTMLElement {
  const section = document.createElement("section");
  section.className = "management-role-definitions";
  section.append(heading("Project role definitions"));

  if (!roleDefinitions) {
    section.append(emptyPanel("Project role definitions are not loaded"));
    return section;
  }

  section.append(managementCountGrid([
    ["Custom roles", roleDefinitions.roles.length],
    ["Active", roleDefinitions.activeCount],
    ["Disabled", roleDefinitions.disabledCount]
  ]));

  const roleList = document.createElement("div");
  roleList.className = "management-role-list";
  if (roleDefinitions.roles.length === 0) {
    roleList.append(line("Default role templates only", "memory-meta"));
  } else {
    for (const role of roleDefinitions.roles) {
      roleList.append(managementProjectRoleDefinitionCard(role));
    }
  }

  section.append(roleList, managementNewProjectRoleDefinitionForm(roleDefinitions.project.projectId));
  return section;
}

function managementProjectRoleDefinitionCard(role: AdminManagementProjectRoleDefinition): HTMLElement {
  const card = document.createElement("section");
  card.className = "management-role-definition";
  card.append(
    line(`${role.displayName} · ${role.roleId}`, "management-access-title"),
    pillRow([role.status, role.templateRoleId ?? "custom"]),
    detailGrid([
      ["Role assignments", role.assignmentCount.toString()],
      ["Role grants", role.roleGrantCount.toString()],
      ["Updated", shortDate(role.updatedAt)]
    ]));

  if (role.description) {
    card.append(line(role.description, "management-access-meta"));
  }

  if (role.status === "active" && (role.assignmentCount > 0 || role.roleGrantCount > 0)) {
    card.append(line(
      "Disable after removing dependent role assignments and role-targeted grants.",
      "management-access-prompt"));
  }

  const form = document.createElement("form");
  form.className = "access-form management-role-form";
  form.append(
    managementReadOnlyField("Role id", role.roleId),
    managementInputField("Display name", "displayName", "Role display name", true, role.displayName),
    managementTextAreaField("Description", "description", "Role purpose and responsibility", false, role.description ?? ""),
    managementSelectField("Template", "templateRoleId", managementTemplateRoleOptions(), role.templateRoleId ?? ""),
    managementSelectField("Status", "status", managementRoleStatusOptions(), role.status),
    managementInputField("Reason", "reason", "Role definition change reason", true),
    managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "primary-action";
  button.textContent = "Update Role";
  button.disabled = state.busy;
  form.append(button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    void updateManagementProjectRoleDefinition(role.projectId, role.roleId, form);
  });

  card.append(form);
  return card;
}

function managementNewProjectRoleDefinitionForm(projectId: string): HTMLElement {
  const form = document.createElement("form");
  form.className = "access-form management-role-form management-role-create-form";
  form.append(
    heading("New project role"),
    managementInputField("Role id", "roleId", "delivery_lead", true),
    managementInputField("Display name", "displayName", "Delivery Lead", true),
    managementTextAreaField("Description", "description", "Role purpose and responsibility", false),
    managementSelectField("Template", "templateRoleId", managementTemplateRoleOptions(), ""),
    managementSelectField("Status", "status", managementRoleStatusOptions(), "active"),
    managementInputField("Reason", "reason", "Role definition reason", true),
    managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "primary-action";
  button.textContent = "Create Role";
  button.disabled = state.busy;
  form.append(button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    const roleId = formValue(form, "roleId");
    void updateManagementProjectRoleDefinition(projectId, roleId, form);
  });

  return form;
}

function managementTemplateRoleOptions(): Array<[string, string]> {
  return [
    ["", "No template"],
    ["product_owner", "Product Owner"],
    ["cto", "CTO"],
    ["security_professional", "Security Professional"],
    ["it_manager", "IT Manager"],
    ["developer", "Developer"],
    ["tester_qa", "Tester/QA"],
    ["release_manager", "Release Manager"],
    ["knowledge_steward", "Knowledge Steward"],
    ["designer", "Designer"],
    ["cfo", "CFO"],
    ["coo", "COO"],
    ["ceo", "CEO"]
  ];
}

function managementRoleStatusOptions(): Array<[string, string]> {
  return [
    ["active", "Active"],
    ["disabled", "Disabled"]
  ];
}

function managementGrantMatrixSection(matrix: AdminManagementGrantMatrixResponse | null): HTMLElement {
  const section = document.createElement("section");
  section.className = "management-grant-matrix";
  section.append(heading("Grant matrix"));

  if (!matrix) {
    section.append(emptyPanel("Grant matrix is not loaded"));
    return section;
  }

  const grantCount = matrix.roles.reduce((sum, role) => sum + role.grants.length, 0);
  const previewCount = matrix.roles.reduce((sum, role) => sum + role.effectiveAccessPreviews.length, 0);
  section.append(managementCountGrid([
    ["Roles", matrix.roles.length],
    ["Presets", matrix.presets.length],
    ["Role grants", grantCount],
    ["Previews", previewCount]
  ]));

  const roles = document.createElement("div");
  roles.className = "management-grant-roles";
  for (const role of matrix.roles) {
    roles.append(managementGrantMatrixRoleCard(matrix, role));
  }

  section.append(roles);
  return section;
}

function managementGrantMatrixRoleCard(
  matrix: AdminManagementGrantMatrixResponse,
  role: AdminManagementGrantMatrixRole): HTMLElement {
  const card = document.createElement("section");
  card.className = "management-grant-role";
  card.append(
    line(`${role.displayName} · ${role.roleId}`, "management-access-title"),
    line(`${role.status} · ${role.recommendedPresetId} · ${grantMatrixAlignmentLabel(role.presetAlignment)}`, "management-access-meta"));

  if (role.description) {
    card.append(line(role.description, "management-access-meta"));
  }

  const grants = document.createElement("div");
  grants.className = "management-grant-list";
  if (role.grants.length === 0) {
    grants.append(line("No role-targeted grants", "memory-meta"));
  } else {
    for (const grant of role.grants) {
      grants.append(line(`${grant.permission} · ${grant.namespacePrefix}`, "management-grant-row"));
    }
  }

  const previews = document.createElement("div");
  previews.className = "management-grant-previews";
  const allowedPreviewCount = role.effectiveAccessPreviews.filter(preview => preview.allowed).length;
  previews.append(line(`${allowedPreviewCount}/${role.effectiveAccessPreviews.length} effective previews allowed`, "management-access-meta"));
  for (const preview of role.effectiveAccessPreviews.slice(0, 4)) {
    previews.append(line(
      `${preview.allowed ? "allowed" : "denied"} · ${preview.permission} · ${preview.namespacePrefix}`,
      preview.allowed ? "management-grant-preview-good" : "management-grant-preview-warn"));
  }

  const form = document.createElement("form");
  form.className = "access-form management-grant-form";
  const presetField = managementSelectField(
    "Preset",
    "presetId",
    [
      ["custom", "Custom"],
      ...matrix.presets.map(preset => [preset.presetId, preset.displayName] as [string, string])
    ],
    role.presetAlignment === "matches_preset" ? role.recommendedPresetId : "custom");
  const grantRowsField = managementTextAreaField(
    "Grant rows",
    "grantRows",
    "read /project/.../facts",
    true,
    grantMatrixRowsText(role.grants));
  const grantRows = grantRowsField.querySelector("textarea")!;
  const presetSelect = presetField.querySelector("select")!;
  presetSelect.addEventListener("change", () => {
    const preset = matrix.presets.find(candidate => candidate.presetId === presetSelect.value);
    if (preset) {
      grantRows.value = grantMatrixPresetRowsText(matrix.project.projectId, role.roleId, preset);
    }
  });

  form.append(
    presetField,
    grantRowsField,
    managementInputField("Reason", "reason", "Grant matrix reason", true),
    managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "primary-action";
  button.textContent = "Update Matrix";
  button.disabled = state.busy;
  form.append(button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    void updateManagementGrantMatrixRole(matrix.project.projectId, role, form);
  });

  card.append(grants, previews, form);
  return card;
}

function managementSuccessBenchmarkSection(): HTMLElement {
  const section = document.createElement("section");
  section.className = "management-success-benchmark";
  section.append(
    heading("Management success benchmark"),
    detailGrid(managementSuccessBenchmarkSummaryRows()),
    managementSuccessBenchmarkItemsList(),
    managementSuccessBenchmarkControls());
  return section;
}

function managementSuccessBenchmarkItemsList(): HTMLElement {
  const list = document.createElement("div");
  list.className = "audit-list";

  for (const item of managementSuccessBenchmarkItems()) {
    const row = document.createElement("div");
    row.className = "audit-row";
    row.append(
      line(item.label, "memory-title"),
      pillRow([item.status]),
      line(item.detail, "memory-meta"));
    list.append(row);
  }

  return list;
}

function managementSuccessBenchmarkControls(): HTMLElement {
  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(
    managementReadOnlyField("Benchmark started", state.management.benchmarkStartedAt || "pending"),
    managementReadOnlyField("First detail loaded", state.management.benchmarkFirstDetailLoadedAt || "pending"),
    managementBenchmarkSelectField(
      "Access drift",
      state.management.benchmarkAccessDriftStatus,
      ["pending", "clear", "drift_found"],
      value => {
        state.management.benchmarkAccessDriftStatus = value === "clear" || value === "drift_found" ? value : "pending";
        render();
      }),
    managementBenchmarkSelectField(
      "SQL fallback",
      state.management.benchmarkSqlFallbackStatus,
      ["none", "used"],
      value => {
        state.management.benchmarkSqlFallbackStatus = value === "used" ? "used" : "none";
        render();
      }),
    managementBenchmarkSelectField(
      "Confidence",
      state.management.benchmarkUserConfidenceScore,
      ["", "1", "2", "3", "4", "5"],
      value => {
        state.management.benchmarkUserConfidenceScore = ["1", "2", "3", "4", "5"].includes(value) ? value : "";
        render();
      }));
  return grid;
}

function managementSuccessBenchmarkItems(): AdminManagementBenchmarkItem[] {
  const findInspectDuration = managementFindInspectDurationSeconds();
  const modificationDuration = managementModificationDurationSeconds();
  const accessDriftFindingCount = managementAccessDriftFindingCount();
  const rawPayloadLeakageCount = managementRawPayloadLeakageCount();
  const confidenceScore = managementUserConfidenceScoreValue();

  return [
    {
      id: "management_find_inspect",
      label: "Find and inspect",
      status: state.management.benchmarkFirstDetailLoadedAt ? "done" : "needs_review",
      detail: findInspectDuration === null ? "detail not loaded" : `${findInspectDuration} seconds`
    },
    {
      id: "management_safe_modify",
      label: "Safe modification",
      status: state.management.benchmarkLastModificationCompletedAt ? "done" : "needs_review",
      detail: state.management.benchmarkLatestOperation
        ? `${state.management.benchmarkLatestOperation} in ${modificationDuration ?? 0} seconds`
        : "no management modification recorded"
    },
    {
      id: "management_operation_failures",
      label: "Operation failures",
      status: state.management.benchmarkOperationFailureCount === 0 ? "done" : "needs_review",
      detail: `${state.management.benchmarkOperationFailureCount} failed management operation${state.management.benchmarkOperationFailureCount === 1 ? "" : "s"}`
    },
    {
      id: "management_access_drift",
      label: "Access drift",
      status: state.management.benchmarkAccessDriftStatus === "clear"
        ? "done"
        : state.management.benchmarkAccessDriftStatus === "drift_found" ? "blocked" : "needs_review",
      detail: accessDriftFindingCount === null ? "pending" : `${accessDriftFindingCount} finding${accessDriftFindingCount === 1 ? "" : "s"}`
    },
    {
      id: "management_sql_fallback",
      label: "SQL fallback",
      status: state.management.benchmarkSqlFallbackStatus === "none" ? "done" : "blocked",
      detail: state.management.benchmarkSqlFallbackStatus
    },
    {
      id: "management_raw_payloads",
      label: "Raw payload leakage",
      status: rawPayloadLeakageCount === 0 ? "done" : "blocked",
      detail: `${rawPayloadLeakageCount} raw payload exposure${rawPayloadLeakageCount === 1 ? "" : "s"}`
    },
    {
      id: "management_user_confidence",
      label: "User confidence",
      status: confidenceScore >= 4 ? "done" : "needs_review",
      detail: managementUserConfidenceLabel()
    }
  ];
}

function managementSuccessBenchmarkSummaryRows(): [string, string][] {
  return [
    ["Started", state.management.benchmarkStartedAt || "pending"],
    ["First detail", state.management.benchmarkFirstDetailLoadedAt || "pending"],
    ["Find/inspect duration", formatManagementDuration(managementFindInspectDurationSeconds())],
    ["Last modification", state.management.benchmarkLastModificationCompletedAt || "pending"],
    ["Latest operation", state.management.benchmarkLatestOperation || "pending"],
    ["Activity entries", (state.management.managementActivityDetail?.returnedCount ?? 0).toString()],
    ["Custom roles", (state.management.roleDefinitionsDetail?.roles.length ?? 0).toString()],
    ["Operation failures", state.management.benchmarkOperationFailureCount.toString()],
    ["Access drift", state.management.benchmarkAccessDriftStatus],
    ["SQL fallback", state.management.benchmarkSqlFallbackStatus],
    ["Raw source payloads", managementRawPayloadLeakageCount() === 0 ? "not included" : "review required"],
    ["User confidence", managementUserConfidenceLabel()],
    ["Project-success evidence", managementSuccessBenchmarkStatusText()]
  ];
}

function managementSuccessBenchmarkEvidence(): Record<string, unknown> {
  return {
    benchmarkId: "OPM-06",
    payloadSafe: true,
    rawSourcePayloadsIncluded: false,
    rawMemoryPayloadsIncluded: false,
    projectSuccessEvidenceLoop: true,
    managementStartedAt: state.management.benchmarkStartedAt || null,
    managementFirstDetailLoadedAt: state.management.benchmarkFirstDetailLoadedAt || null,
    managementLastModificationCompletedAt: state.management.benchmarkLastModificationCompletedAt || null,
    managementFindInspectDurationSeconds: managementFindInspectDurationSeconds(),
    managementModificationDurationSeconds: managementModificationDurationSeconds(),
    managementOperationFailureCount: state.management.benchmarkOperationFailureCount,
    latestOperation: state.management.benchmarkLatestOperation || null,
    visibleOrganizationCount: state.management.organizations.length,
    visibleProjectCount: state.management.projects.length,
    managedProjectRoleDefinitionCount: state.management.roleDefinitionsDetail?.roles.length ?? 0,
    managementActivityEntryCount: state.management.managementActivityDetail?.returnedCount ?? 0,
    selectedScope: managementSelectedScopeEvidence(),
    accessDriftStatus: state.management.benchmarkAccessDriftStatus,
    accessDriftFindingCount: managementAccessDriftFindingCount(),
    sqlFallbackStatus: state.management.benchmarkSqlFallbackStatus,
    sqlFallbackCount: state.management.benchmarkSqlFallbackStatus === "used" ? 1 : 0,
    rawPayloadLeakageCount: managementRawPayloadLeakageCount(),
    userConfidence: managementUserConfidenceScoreValue() || null,
    userConfidenceScore: managementUserConfidenceScoreValue() || null,
    organizationProjectManagement: {
      managementFindInspectDurationSeconds: managementFindInspectDurationSeconds(),
      managementModificationDurationSeconds: managementModificationDurationSeconds(),
      managementOperationFailureCount: state.management.benchmarkOperationFailureCount,
      accessDriftFindingCount: managementAccessDriftFindingCount(),
      sqlFallbackCount: state.management.benchmarkSqlFallbackStatus === "used" ? 1 : 0,
      rawPayloadLeakageCount: managementRawPayloadLeakageCount(),
      managementActivityEntryCount: state.management.managementActivityDetail?.returnedCount ?? 0,
      managedProjectRoleDefinitionCount: state.management.roleDefinitionsDetail?.roles.length ?? 0,
      userConfidence: managementUserConfidenceScoreValue() || null
    },
    checks: managementSuccessBenchmarkItems(),
    status: managementSuccessBenchmarkStatusText()
  };
}

function managementLifecycleForm(project: AdminManagementProjectSummary): HTMLElement {
  const form = document.createElement("form");
  form.className = "access-form management-form";
  form.append(heading("Lifecycle"));

  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(
    managementSelectField("Project status", "projectStatus", [
      ["planned", "Planned"],
      ["active", "Active"],
      ["archived", "Archived"],
      ["deleted", "Deleted"]
    ], project.projectStatus),
    managementInputField("Reason", "reason", "Lifecycle change reason", true),
    managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "primary-action";
  button.textContent = "Update Lifecycle";
  button.disabled = state.busy;
  form.append(grid, button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    void updateManagementLifecycle(project.projectId, form);
  });

  return form;
}

function managementScopeSettingsForm(
  project: AdminManagementProjectSummary,
  scopeSettings: AdminManagementScopeSettings | undefined): HTMLElement {
  const form = document.createElement("form");
  form.className = "access-form management-form";
  form.append(heading("Scope settings"));

  const defaultNamespacePrefix = scopeSettings?.defaultNamespacePrefix ?? `/project/${project.projectId}/facts`;
  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(
    managementInputField("Default namespace", "defaultNamespacePrefix", `/project/${project.projectId}/facts`, true, defaultNamespacePrefix),
    managementCheckboxField("Source hash required", "sourceHashRequired", scopeSettings?.sourceHashRequired ?? true),
    managementSelectField("Retention", "memoryRetentionClass", [
      ["ephemeral", "Ephemeral"],
      ["standard", "Standard"],
      ["audit", "Audit"],
      ["legal_hold", "Legal hold"]
    ], scopeSettings?.memoryRetentionClass ?? "standard"),
    managementNumberField("Review cadence", "reviewCadenceDays", scopeSettings?.reviewCadenceDays ?? 7),
    managementInputField("Reason", "reason", "Scope settings reason", true),
    managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));

  const button = document.createElement("button");
  button.type = "submit";
  button.className = "primary-action";
  button.textContent = "Update Settings";
  button.disabled = state.busy;
  form.append(grid, button);
  form.addEventListener("submit", event => {
    event.preventDefault();
    void updateManagementScopeSettings(project.projectId, form);
  });

  return form;
}

function managementInputField(
  labelText: string,
  name: string,
  placeholder: string,
  required: boolean,
  value = ""): HTMLLabelElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  span.textContent = labelText;
  const input = document.createElement("input");
  input.name = name;
  input.placeholder = placeholder;
  input.required = required;
  input.value = value;
  label.append(span, input);
  return label;
}

function managementTextAreaField(
  labelText: string,
  name: string,
  placeholder: string,
  required: boolean,
  value = ""): HTMLLabelElement {
  const label = document.createElement("label");
  label.className = "management-wide-field";
  const span = document.createElement("span");
  span.textContent = labelText;
  const textarea = document.createElement("textarea");
  textarea.name = name;
  textarea.placeholder = placeholder;
  textarea.required = required;
  textarea.rows = 5;
  textarea.value = value;
  label.append(span, textarea);
  return label;
}

function managementNumberField(labelText: string, name: string, value: number): HTMLLabelElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  span.textContent = labelText;
  const input = document.createElement("input");
  input.name = name;
  input.type = "number";
  input.min = "1";
  input.max = "365";
  input.required = true;
  input.value = value.toString();
  label.append(span, input);
  return label;
}

function managementSelectField(
  labelText: string,
  name: string,
  options: Array<[string, string]>,
  selectedValue: string): HTMLLabelElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  span.textContent = labelText;
  const select = document.createElement("select");
  select.name = name;
  for (const [value, text] of options) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = text;
    option.selected = value === selectedValue;
    select.append(option);
  }

  label.append(span, select);
  return label;
}

function managementCheckboxField(labelText: string, name: string, checked: boolean): HTMLLabelElement {
  const label = document.createElement("label");
  label.className = "management-check-field";
  const input = document.createElement("input");
  input.name = name;
  input.type = "checkbox";
  input.checked = checked;
  const span = document.createElement("span");
  span.textContent = labelText;
  label.append(input, span);
  return label;
}

function managementReadOnlyField(labelText: string, value: string): HTMLLabelElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  span.textContent = labelText;
  const input = document.createElement("input");
  input.readOnly = true;
  input.value = value;
  label.append(span, input);
  return label;
}

function managementBenchmarkSelectField(
  labelText: string,
  value: string,
  values: string[],
  onChange: (value: string) => void): HTMLLabelElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  span.textContent = labelText;
  const select = document.createElement("select");
  for (const optionValue of values) {
    const option = document.createElement("option");
    option.value = optionValue;
    option.textContent = optionValue || "pending";
    option.selected = optionValue === value;
    select.append(option);
  }

  select.addEventListener("change", () => onChange(select.value));
  label.append(span, select);
  return label;
}

function managementSection(title: string, rows: HTMLElement[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "management-section";
  section.append(line(title, "management-section-title"), ...rows);
  return section;
}

function renderManagementOrganizationRow(organization: AdminManagementOrganizationSummary): HTMLElement {
  const button = document.createElement("button");
  button.type = "button";
  button.className = isSelectedManagementItem("organization", organization.organizationId)
    ? "memory-row selected"
    : "memory-row";
  button.addEventListener("click", () => {
    state.management.selectedItem = { type: "organization", id: organization.organizationId };
    state.selectedSource = null;
    void loadSelectedManagementDetail(true);
  });

  button.append(
    line(organization.organizationName, "memory-title"),
    pillRow([organization.actorAccessLevel]),
    line(`${organization.projectCount} projects · ${organization.organizationMembershipCount} org members`, "memory-meta"),
    line(managementProjectStatusText(organization.projectStatusCounts), "memory-date"));
  return button;
}

function renderManagementProjectRow(project: AdminManagementProjectSummary): HTMLElement {
  const button = document.createElement("button");
  button.type = "button";
  button.className = isSelectedManagementItem("project", project.projectId)
    ? "memory-row selected"
    : "memory-row";
  button.addEventListener("click", () => {
    state.management.selectedItem = { type: "project", id: project.projectId };
    state.selectedSource = null;
    void loadSelectedManagementDetail(true);
  });

  button.append(
    line(project.projectName, "memory-title"),
    pillRow([project.projectStatus, project.actorAccessLevel]),
    line(project.organizationName, "memory-meta"),
    line(`${project.projectMembershipCount} members · ${project.namespaceGrantCount} grants`, "memory-date"));
  return button;
}

function managementCountGrid(items: Array<[string, number]>): HTMLElement {
  const grid = document.createElement("div");
  grid.className = "management-count-grid";

  for (const [label, value] of items) {
    const item = document.createElement("div");
    item.className = "management-count";
    item.append(line(value.toString(), "management-count-value"), line(label, "management-count-label"));
    grid.append(item);
  }

  return grid;
}

function managementActions(): HTMLElement[] {
  return [
    managementModeButton("Project Registration", "registration"),
    managementModeButton("Access Management", "access"),
    managementRefreshButton()
  ];
}

function managementModeButton(label: string, mode: AdminConsoleState["mode"]): HTMLButtonElement {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "secondary-action";
  button.textContent = label;
  button.disabled = state.busy;
  button.addEventListener("click", () => switchManagementMode(mode));
  return button;
}

function managementRefreshButton(): HTMLButtonElement {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "primary-action";
  button.textContent = "Refresh";
  button.disabled = state.busy;
  button.addEventListener("click", () => void loadManagement());
  return button;
}

function switchManagementMode(mode: AdminConsoleState["mode"]): void {
  state.mode = mode;
  elements.modeFilter.value = mode;
  state.selectedSource = null;
  updateFilterVisibility();
  render();
  void loadCurrentMode();
}

function managementLinks(): AdminComplianceStatusLink[] {
  return [
    {
      label: "Organizations API",
      href: "/api/admin/organizations",
      kind: "api",
      method: "GET"
    },
    {
      label: "Projects API",
      href: "/api/admin/projects",
      kind: "api",
      method: "GET"
    },
    {
      label: "Project lifecycle API",
      href: "/api/admin/projects/{projectId}/lifecycle",
      kind: "api",
      method: "PATCH"
    },
    {
      label: "Project scope settings API",
      href: "/api/admin/projects/{projectId}/scope-settings",
      kind: "api",
      method: "GET/PUT"
    },
    {
      label: "Access inventory API",
      href: "/api/admin/{organizations|projects}/{id}/access-inventory",
      kind: "api",
      method: "GET"
    },
    {
      label: "Access revocation API",
      href: "/api/admin/access/revocations",
      kind: "api",
      method: "POST"
    },
    {
      label: "Grant matrix API",
      href: "/api/admin/projects/{projectId}/grant-matrix",
      kind: "api",
      method: "GET/PUT"
    },
    {
      label: "Project role definitions API",
      href: "/api/admin/projects/{projectId}/role-definitions",
      kind: "api",
      method: "GET/PUT"
    },
    {
      label: "Management activity API",
      href: "/api/admin/{organizations|projects}/{id}/management-activity",
      kind: "api",
      method: "GET"
    },
    {
      label: "Project Registration",
      href: "/admin/",
      kind: "ui",
      method: "GET"
    },
    {
      label: "Access Management",
      href: "/admin/",
      kind: "ui",
      method: "GET"
    }
  ];
}

function ensureManagementSelection(): void {
  const selected = state.management.selectedItem;

  if (selected && isVisibleManagementSelection(selected)) {
    return;
  }

  const firstOrganization = state.management.organizations[0];
  if (firstOrganization) {
    state.management.selectedItem = {
      type: "organization",
      id: firstOrganization.organizationId
    };
    return;
  }

  const firstProject = state.management.projects[0];
  state.management.selectedItem = firstProject
    ? {
        type: "project",
        id: firstProject.projectId
      }
    : null;
}

function isVisibleManagementSelection(selection: AdminManagementSelection): boolean {
  if (selection.type === "organization") {
    return state.management.organizations.some(organization => organization.organizationId === selection.id);
  }

  return state.management.projects.some(project => project.projectId === selection.id);
}

function isSelectedManagementItem(type: AdminManagementItemType, id: string): boolean {
  const selected = state.management.selectedItem;
  return selected?.type === type && selected.id === id;
}

function managementProjectStatusText(counts: AdminManagementProjectStatusCounts): string {
  return `${counts.active} active · ${counts.planned} planned · ${counts.archived} archived · ${counts.deleted} deleted`;
}

function ensureManagementBenchmarkStarted(): void {
  if (!state.management.benchmarkStartedAt) {
    state.management.benchmarkStartedAt = new Date().toISOString();
  }
}

function markManagementDetailLoaded(): void {
  ensureManagementBenchmarkStarted();
  if (!state.management.benchmarkFirstDetailLoadedAt) {
    state.management.benchmarkFirstDetailLoadedAt = new Date().toISOString();
  }
}

function markManagementModificationCompleted(operation: string): void {
  ensureManagementBenchmarkStarted();
  state.management.benchmarkLastModificationCompletedAt = new Date().toISOString();
  state.management.benchmarkLatestOperation = operation;
}

function markManagementOperationFailed(): void {
  ensureManagementBenchmarkStarted();
  state.management.benchmarkOperationFailureCount += 1;
}

function managementSuccessBenchmarkStatusText(): string {
  const items = managementSuccessBenchmarkItems();
  if (items.some(item => item.status === "blocked")) {
    return "blocked";
  }

  return items.some(item => item.status === "needs_review") ? "needs_review" : "done";
}

function managementSelectedScopeEvidence(): Record<string, unknown> | null {
  const selected = state.management.selectedItem;
  if (!selected) {
    return null;
  }

  return {
    scopeType: selected.type,
    scopeId: selected.id,
    projectStatus: state.management.projectDetail?.project.projectStatus ?? null
  };
}

function managementFindInspectDurationSeconds(): number | null {
  return managementSecondsBetween(
    state.management.benchmarkStartedAt,
    state.management.benchmarkFirstDetailLoadedAt);
}

function managementModificationDurationSeconds(): number | null {
  return managementSecondsBetween(
    state.management.benchmarkStartedAt,
    state.management.benchmarkLastModificationCompletedAt);
}

function managementSecondsBetween(startValue: string, endValue: string): number | null {
  if (!startValue || !endValue) {
    return null;
  }

  const start = Date.parse(startValue);
  const end = Date.parse(endValue);
  if (!Number.isFinite(start) || !Number.isFinite(end) || end < start) {
    return null;
  }

  return Math.round((end - start) / 1000);
}

function formatManagementDuration(seconds: number | null): string {
  if (seconds === null) {
    return "pending";
  }

  if (seconds < 60) {
    return `${seconds}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  if (minutes < 60) {
    return `${minutes}m ${remainingSeconds}s`;
  }

  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

function managementAccessDriftFindingCount(): number | null {
  if (state.management.benchmarkAccessDriftStatus === "pending") {
    return null;
  }

  if (state.management.benchmarkAccessDriftStatus === "clear") {
    return 0;
  }

  return Math.max(1, state.management.accessInventoryDetail?.counts.staleAccessPrompts ?? 0);
}

function managementRawPayloadLeakageCount(): number {
  const detailFlags = [
    state.management.organizationDetail?.rawSourcePayloadsIncluded,
    state.management.projectDetail?.rawSourcePayloadsIncluded,
    state.management.scopeSettingsDetail?.rawSourcePayloadsIncluded,
    state.management.accessInventoryDetail?.rawSourcePayloadsIncluded,
    state.management.roleDefinitionsDetail?.rawSourcePayloadsIncluded,
    state.management.grantMatrixDetail?.rawSourcePayloadsIncluded,
    state.management.managementActivityDetail?.rawSourcePayloadsIncluded
  ];

  return detailFlags.filter(flag => flag === true).length;
}

function managementUserConfidenceScoreValue(): number {
  const score = Number.parseInt(state.management.benchmarkUserConfidenceScore, 10);
  return Number.isInteger(score) && score >= 1 && score <= 5 ? score : 0;
}

function managementUserConfidenceLabel(): string {
  const score = managementUserConfidenceScoreValue();
  return score === 0 ? "pending" : `${score}/5`;
}

function grantMatrixAlignmentLabel(alignment: string): string {
  switch (alignment) {
    case "matches_preset":
      return "matches preset";
    case "missing_preset_grants":
      return "missing preset grants";
    case "has_extra_grants":
      return "has extra grants";
    default:
      return "custom";
  }
}

function grantMatrixRowsText(grants: AdminManagementGrantMatrixGrant[]): string {
  return grants
    .map(grant => `${grant.permission} ${grant.namespacePrefix}`)
    .join("\n");
}

function grantMatrixPresetRowsText(
  projectId: string,
  roleId: string,
  preset: AdminManagementGrantMatrixPreset): string {
  return preset.grants
    .map(grant => `${grant.permission} ${resolveGrantMatrixTemplate(projectId, roleId, grant.namespaceTemplate)}`)
    .join("\n");
}

function resolveGrantMatrixTemplate(projectId: string, roleId: string, namespaceTemplate: string): string {
  return namespaceTemplate
    .replaceAll("{projectRoot}", `/project/${projectId}`)
    .replaceAll("{roleId}", roleId);
}

function parseGrantMatrixRows(value: string): Array<{ namespacePrefix: string; permission: string }> {
  const rows: Array<{ namespacePrefix: string; permission: string }> = [];
  const seen = new Set<string>();
  const lines = value.split(/\r?\n/);

  for (let index = 0; index < lines.length; index++) {
    const lineValue = lines[index].trim();
    if (!lineValue) {
      continue;
    }

    const parts = lineValue.split(/\s+/);
    if (parts.length < 2) {
      throw new Error(`Grant row ${index + 1} must use "permission namespace".`);
    }

    const permission = parts[0].toLowerCase();
    const namespacePrefix = parts.slice(1).join(" ");
    const key = `${permission}\n${namespacePrefix}`;
    if (seen.has(key)) {
      throw new Error(`Grant row ${index + 1} duplicates an earlier permission and namespace.`);
    }

    seen.add(key);
    rows.push({ namespacePrefix, permission });
  }

  return rows;
}

async function updateManagementLifecycle(projectId: string, form: HTMLFormElement): Promise<void> {
  setBusy(true);
  setStatus("Saving lifecycle");

  try {
    const response = await apiFetch<AdminManagementLifecycleUpdateResponse>(
      `/api/admin/projects/${projectId}/lifecycle`,
      {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          projectStatus: formValue(form, "projectStatus"),
          reason: formValue(form, "reason"),
          auditEvidenceId: formValue(form, "auditEvidenceId")
        })
    });

    applyManagementLifecycleResponse(response);
    await refreshManagementActivityDetailBestEffort();
    markManagementModificationCompleted("project_lifecycle");
    state.management.evidencePayload = response;
    setStatus("Lifecycle updated");
  } catch (error) {
    markManagementOperationFailed();
    state.management.evidencePayload = { error: errorMessage(error) };
    setStatus("Lifecycle error");
  } finally {
    setBusy(false);
    render();
  }
}

async function updateManagementGrantMatrixRole(
  projectId: string,
  role: AdminManagementGrantMatrixRole,
  form: HTMLFormElement): Promise<void> {
  setBusy(true);
  setStatus("Saving grant matrix");

  try {
    const response = await apiFetch<AdminManagementGrantMatrixUpdateResponse>(
      `/api/admin/projects/${projectId}/grant-matrix/roles/${encodeURIComponent(role.roleId)}`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          roleId: role.roleId,
          presetId: formValue(form, "presetId"),
          grants: parseGrantMatrixRows(formValue(form, "grantRows")),
          reason: formValue(form, "reason"),
          auditEvidenceId: formValue(form, "auditEvidenceId")
        })
      });

    await refreshManagementDirectory();
    await loadSelectedManagementDetail(false);
    markManagementModificationCompleted("grant_matrix");
    state.management.evidencePayload = response;
    setStatus("Grant matrix updated");
  } catch (error) {
    markManagementOperationFailed();
    state.management.evidencePayload = { error: errorMessage(error) };
    setStatus("Grant matrix error");
  } finally {
    setBusy(false);
    render();
  }
}

async function updateManagementProjectRoleDefinition(
  projectId: string,
  roleId: string,
  form: HTMLFormElement): Promise<void> {
  setBusy(true);
  setStatus("Saving role definition");

  try {
    const response = await apiFetch<AdminManagementProjectRoleDefinitionUpdateResponse>(
      `/api/admin/projects/${projectId}/role-definitions/${encodeURIComponent(roleId)}`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          roleId,
          displayName: formValue(form, "displayName"),
          description: formValue(form, "description") || null,
          templateRoleId: formValue(form, "templateRoleId") || null,
          status: formValue(form, "status"),
          reason: formValue(form, "reason"),
          auditEvidenceId: formValue(form, "auditEvidenceId")
        })
      });

    await refreshManagementDirectory();
    await loadSelectedManagementDetail(false);
    markManagementModificationCompleted("role_definition");
    state.management.evidencePayload = response;
    setStatus("Role definition updated");
  } catch (error) {
    markManagementOperationFailed();
    state.management.evidencePayload = { error: errorMessage(error) };
    setStatus("Role definition error");
  } finally {
    setBusy(false);
    render();
  }
}

async function updateManagementScopeSettings(projectId: string, form: HTMLFormElement): Promise<void> {
  setBusy(true);
  setStatus("Saving settings");

  try {
    const response = await apiFetch<AdminManagementScopeSettingsUpdateResponse>(
      `/api/admin/projects/${projectId}/scope-settings`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          defaultNamespacePrefix: formValue(form, "defaultNamespacePrefix"),
          sourceHashRequired: (form.elements.namedItem("sourceHashRequired") as HTMLInputElement | null)?.checked ?? false,
          memoryRetentionClass: formValue(form, "memoryRetentionClass"),
          reviewCadenceDays: Number(formValue(form, "reviewCadenceDays")),
          reason: formValue(form, "reason"),
          auditEvidenceId: formValue(form, "auditEvidenceId")
        })
      });

    state.management.scopeSettingsDetail = {
      contractId: response.contractId,
      project: response.project,
      scopeSettings: response.scopeSettings,
      payloadSafe: response.payloadSafe,
      rawSourcePayloadsIncluded: response.rawSourcePayloadsIncluded
    };
    await refreshManagementActivityDetailBestEffort();
    markManagementModificationCompleted("scope_settings");
    state.management.evidencePayload = response;
    setStatus("Settings updated");
  } catch (error) {
    markManagementOperationFailed();
    state.management.evidencePayload = { error: errorMessage(error) };
    setStatus("Settings error");
  } finally {
    setBusy(false);
    render();
  }
}

async function revokeManagementAccess(
  target: AdminManagementRevocationTarget,
  form: HTMLFormElement): Promise<void> {
  setBusy(true);
  setStatus("Revoking access");

  try {
    const response = await apiFetch<AdminManagementAccessRevocationResponse>(
      "/api/admin/access/revocations",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          scopeType: target.scopeType,
          scopeId: target.scopeId,
          accessRecordType: target.accessRecordType,
          accessRecordId: target.accessRecordId,
          principalId: target.principalId,
          reason: formValue(form, "reason"),
          auditEvidenceId: formValue(form, "auditEvidenceId")
        })
      });

    await refreshManagementDirectory();
    await loadSelectedManagementDetail(false);
    markManagementModificationCompleted("access_revocation");
    state.management.evidencePayload = response;
    setStatus("Access revoked");
  } catch (error) {
    markManagementOperationFailed();
    state.management.evidencePayload = { error: errorMessage(error) };
    setStatus("Revocation error");
  } finally {
    setBusy(false);
    render();
  }
}

function applyManagementLifecycleResponse(response: AdminManagementLifecycleUpdateResponse): void {
  for (const project of state.management.projects) {
    if (project.projectId === response.project.projectId) {
      project.projectStatus = response.project.projectStatus;
      project.updatedAt = response.auditEvidence.occurredAt;
    }
  }

  if (state.management.projectDetail?.project.projectId === response.project.projectId) {
    state.management.projectDetail.project.projectStatus = response.project.projectStatus;
    state.management.projectDetail.project.updatedAt = response.auditEvidence.occurredAt;
  }
}
