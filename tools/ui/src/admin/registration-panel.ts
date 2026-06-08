type RegistrationStepId = "scope" | "owners_roles" | "grants" | "seed_docs" | "preflight" | "register" | "closeout";

type RegistrationValidationStatus = "done" | "blocked" | "needs_review";
type RegistrationSeedContextCheckStatus = "pending" | "passed" | "failed";
type RegistrationSeedFeedbackStatus = "pending" | "recorded";
type RegistrationAccessDriftStatus = "pending" | "clear" | "drift_found";

interface RegistrationStep {
  id: RegistrationStepId;
  title: string;
}

interface RegistrationOwnerDraft {
  principalId: string;
  roleId: string;
  projectAccessLevel: string;
  principalLabel: string;
}

interface RegistrationRoleDefinitionDraft {
  roleId: string;
  displayName: string;
  description: string;
  templateRoleId: string;
  status: string;
}

interface RegistrationNamespaceGrantDraft {
  targetType: "principal" | "role";
  targetId: string;
  namespaceArea: string;
  namespacePrefix: string;
  permission: string;
}

interface RegistrationSourceDocumentDraft {
  path: string;
  sourceContentSha256: string;
  sourceOwnerRoleId: string;
  memoryTypes: string[];
}

interface RegistrationDraft {
  organizationId: string;
  organizationName: string;
  projectId: string;
  projectName: string;
  projectStatus: string;
  roleDefinitions: RegistrationRoleDefinitionDraft[];
  ownerAssignments: RegistrationOwnerDraft[];
  namespaceGrants: RegistrationNamespaceGrantDraft[];
  sourceDocuments: RegistrationSourceDocumentDraft[];
  seedContextCheckStatus: RegistrationSeedContextCheckStatus;
  seedFeedbackStatus: RegistrationSeedFeedbackStatus;
  accessPreviewReportId: string;
  auditExportId: string;
  registrationNote: string;
  registrationStartedAt: string;
  registrationCompletedAt: string;
  registrationValidationFailureCount: number;
  accessDriftStatus: RegistrationAccessDriftStatus;
  userConfidenceScore: string;
  idempotencyKey: string;
}

interface RegistrationValidationItem {
  id: string;
  label: string;
  status: RegistrationValidationStatus;
  detail: string;
}

interface RegistrationPreviewRequest {
  principalId: string;
  permission: string;
  scopeType: string;
  scopeId: string;
  namespacePrefix: string;
}

const registrationSteps: RegistrationStep[] = [
  { id: "scope", title: "Scope" },
  { id: "owners_roles", title: "Owners/Roles" },
  { id: "grants", title: "Grants" },
  { id: "seed_docs", title: "Seed Docs" },
  { id: "preflight", title: "Preflight" },
  { id: "register", title: "Register" },
  { id: "closeout", title: "Closeout" }
];

const registrationNamespaceAreas = [
  "goals",
  "facts",
  "decisions",
  "rationale",
  "risks",
  "release-evidence"
];

const registrationRequiredOwnerRoleIds = [
  "product_owner",
  "knowledge_steward",
  "security_professional"
];

const registrationMemoryTypes = [
  "goal",
  "target",
  "fact",
  "decision",
  "rationale",
  "risk",
  "assumption",
  "constraint",
  "requirement",
  "release_evidence",
  "role_lens"
];

const registrationDraft: RegistrationDraft = {
  organizationId: "",
  organizationName: "",
  projectId: "",
  projectName: "",
  projectStatus: "active",
  roleDefinitions: [],
  ownerAssignments: [
    {
      principalId: "",
      roleId: "product_owner",
      projectAccessLevel: "contributor",
      principalLabel: "Product Owner"
    },
    {
      principalId: "",
      roleId: "knowledge_steward",
      projectAccessLevel: "reviewer",
      principalLabel: "Knowledge Steward"
    },
    {
      principalId: "",
      roleId: "security_professional",
      projectAccessLevel: "reviewer",
      principalLabel: "Security/Ops"
    }
  ],
  namespaceGrants: [
    {
      targetType: "role",
      targetId: "product_owner",
      namespaceArea: "goals",
      namespacePrefix: "",
      permission: "write"
    },
    {
      targetType: "role",
      targetId: "knowledge_steward",
      namespaceArea: "facts",
      namespacePrefix: "",
      permission: "review"
    },
    {
      targetType: "role",
      targetId: "security_professional",
      namespaceArea: "risks",
      namespacePrefix: "",
      permission: "review"
    }
  ],
  sourceDocuments: [
    {
      path: "",
      sourceContentSha256: "",
      sourceOwnerRoleId: "knowledge_steward",
      memoryTypes: ["fact"]
    }
  ],
  seedContextCheckStatus: "pending",
  seedFeedbackStatus: "pending",
  accessPreviewReportId: "",
  auditExportId: "",
  registrationNote: "",
  registrationStartedAt: new Date().toISOString(),
  registrationCompletedAt: "",
  registrationValidationFailureCount: 0,
  accessDriftStatus: "pending",
  userConfidenceScore: "",
  idempotencyKey: `project-registration-${randomRegistrationId()}`
};

let registrationPreviewResults: Record<string, unknown>[] = [];
let registrationPreviewFingerprint = "";

function renderRegistrationList(): void {
  for (const step of registrationSteps) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = step.id === state.selectedRegistrationStepId ? "memory-row selected" : "memory-row";
    button.addEventListener("click", () => {
      state.selectedRegistrationStepId = step.id;
      state.selectedSource = null;
      render();
    });

    button.append(
      line(step.title, "memory-title"),
      pillRow([registrationStepStatus(step.id)]),
      line(registrationStepSummary(step.id), "memory-meta"));
    elements.resultList.append(button);
  }
}

function renderRegistrationDetail(): void {
  syncRegistrationGrantNamespaces();

  if (state.selectedRegistrationStepId === "scope") {
    renderRegistrationScopeStep();
    return;
  }

  if (state.selectedRegistrationStepId === "owners_roles") {
    renderRegistrationOwnersRolesStep();
    return;
  }

  if (state.selectedRegistrationStepId === "grants") {
    renderRegistrationGrantsStep();
    return;
  }

  if (state.selectedRegistrationStepId === "seed_docs") {
    renderRegistrationSeedDocsStep();
    return;
  }

  if (state.selectedRegistrationStepId === "preflight") {
    renderRegistrationPreflightStep();
    return;
  }

  if (state.selectedRegistrationStepId === "register") {
    renderRegistrationRegisterStep();
    return;
  }

  renderRegistrationCloseoutStep();
}

function renderRegistrationSourceDetail(): void {
  elements.sourceDetail.replaceChildren();

  if (state.registrationResult) {
    const seedReadiness = registrationSeedReadinessEvidence();
    const successBenchmark = registrationSuccessBenchmarkEvidence();
    const json = document.createElement("pre");
    json.className = "source-json";
    json.textContent = JSON.stringify({
      result: state.registrationResult,
      seedReadiness,
      successBenchmark
    }, null, 2);
    elements.sourceDetail.append(
      heading("Registration Result"),
      detailGrid([
        ["Contract", readStringValue(state.registrationResult, "contractId")],
        ["Status", readStringValue(state.registrationResult, "status")],
        ["Payload safe", readBooleanValue(state.registrationResult, "payloadSafe")],
        ["Raw source payloads", readBooleanValue(state.registrationResult, "rawSourcePayloadsIncluded")],
        ["Source hash coverage", `${readNumberValue(state.registrationResult, "sourceHashCoveragePercent")}%`],
        ["Memory types", registrationCoveredMemoryTypes().join(", ")],
        ["Seed readiness", readStringValue(seedReadiness, "status")],
        ["Duration", formatRegistrationDuration()],
        ["Validation failures", registrationDraft.registrationValidationFailureCount.toString()],
        ["Access drift", registrationDraft.accessDriftStatus],
        ["Source-link coverage", `${registrationSourceLinkCoveragePercent()}%`],
        ["User confidence", registrationUserConfidenceLabel()]
      ]),
      json);
    return;
  }

  const previewJson = document.createElement("pre");
  previewJson.className = "source-json";
  previewJson.textContent = JSON.stringify({
    accessPreviewReportId: registrationDraft.accessPreviewReportId || null,
    previewCount: registrationPreviewResults.length,
    previews: registrationPreviewResults,
    validation: registrationValidationItems(),
    seedReadiness: registrationSeedReadinessEvidence(),
    successBenchmark: registrationSuccessBenchmarkEvidence()
  }, null, 2);

  elements.sourceDetail.append(
    heading("Preflight Evidence"),
    detailGrid([
      ["Preview report", registrationDraft.accessPreviewReportId],
      ["Preview rows", registrationPreviewResults.length.toString()],
      ["Hash coverage", `${registrationSourceHashCoveragePercent()}%`],
      ["Source-link coverage", `${registrationSourceLinkCoveragePercent()}%`],
      ["Memory types", registrationCoveredMemoryTypes().join(", ")],
      ["Role-lens readiness", registrationRoleLensReadinessLabel()],
      ["Benchmark status", registrationSuccessBenchmarkStatusText()],
      ["Idempotency key", registrationDraft.idempotencyKey]
    ]),
    previewJson);
}

function renderRegistrationScopeStep(): void {
  const section = registrationSection("Scope", [
    registrationTextField("registration-organization-id", "Org ID", registrationDraft.organizationId, value => {
      registrationDraft.organizationId = value;
      registrationDraftChanged();
    }),
    registrationTextField("registration-organization-name", "Org name", registrationDraft.organizationName, value => {
      registrationDraft.organizationName = value;
      registrationDraftChanged();
    }),
    registrationTextField("registration-project-id", "Project ID", registrationDraft.projectId, value => {
      registrationDraft.projectId = value;
      syncRegistrationGrantNamespaces();
      registrationDraftChanged();
    }),
    registrationTextField("registration-project-name", "Project name", registrationDraft.projectName, value => {
      registrationDraft.projectName = value;
      registrationDraftChanged();
    }),
    registrationSelectField("registration-project-status", "Status", registrationDraft.projectStatus, ["active", "planned"], value => {
      registrationDraft.projectStatus = value;
      registrationDraftChanged();
    }),
    registrationTextField("registration-idempotency-key", "Idempotency key", registrationDraft.idempotencyKey, value => {
      registrationDraft.idempotencyKey = value;
      registrationDraftChanged(false);
    })
  ], [
    registrationButton("Generate IDs", "secondary-action", () => {
      if (!registrationDraft.organizationId) {
        registrationDraft.organizationId = randomRegistrationId();
      }

      if (!registrationDraft.projectId) {
        registrationDraft.projectId = randomRegistrationId();
      }

      registrationDraft.idempotencyKey = `project-registration-${randomRegistrationId()}`;
      syncRegistrationGrantNamespaces();
      registrationDraftChanged();
      render();
    }),
    registrationStepButton("Owners/Roles", "owners_roles")
  ]);

  elements.detail.append(section);
}

function renderRegistrationOwnersRolesStep(): void {
  const section = document.createElement("section");
  section.className = "access-forms";
  section.append(heading("Owners/Roles"));

  const owners = document.createElement("section");
  owners.className = "access-form";
  owners.append(heading("Owner assignments"));

  for (let index = 0; index < registrationDraft.ownerAssignments.length; index += 1) {
    owners.append(registrationOwnerRow(index, registrationDraft.ownerAssignments[index]));
  }

  owners.append(actionRow([
    registrationButton("Add owner", "secondary-action", () => {
      registrationDraft.ownerAssignments.push({
        principalId: "",
        roleId: "developer",
        projectAccessLevel: "contributor",
        principalLabel: ""
      });
      registrationDraftChanged();
      render();
    })
  ]));

  const roles = document.createElement("section");
  roles.className = "access-form";
  roles.append(heading("Custom roles"));

  if (registrationDraft.roleDefinitions.length === 0) {
    roles.append(emptyPanel("No custom roles"));
  }

  for (let index = 0; index < registrationDraft.roleDefinitions.length; index += 1) {
    roles.append(registrationRoleRow(index, registrationDraft.roleDefinitions[index]));
  }

  roles.append(actionRow([
    registrationButton("Add custom role", "secondary-action", () => {
      registrationDraft.roleDefinitions.push({
        roleId: "",
        displayName: "",
        description: "",
        templateRoleId: "",
        status: "active"
      });
      registrationDraftChanged();
      render();
    }),
    registrationStepButton("Grants", "grants")
  ]));

  section.append(owners, roles);
  elements.detail.append(section);
}

function renderRegistrationGrantsStep(): void {
  syncRegistrationGrantNamespaces();

  const section = document.createElement("section");
  section.className = "access-forms";
  section.append(heading("Grants"));

  const grants = document.createElement("section");
  grants.className = "access-form";

  for (let index = 0; index < registrationDraft.namespaceGrants.length; index += 1) {
    grants.append(registrationGrantRow(index, registrationDraft.namespaceGrants[index]));
  }

  grants.append(actionRow([
    registrationButton("Add grant", "secondary-action", () => {
      registrationDraft.namespaceGrants.push({
        targetType: "role",
        targetId: "developer",
        namespaceArea: "facts",
        namespacePrefix: registrationNamespaceFromArea("facts"),
        permission: "read"
      });
      registrationDraftChanged();
      render();
    }),
    registrationStepButton("Seed Docs", "seed_docs")
  ]));

  section.append(grants);
  elements.detail.append(section);
}

function renderRegistrationSeedDocsStep(): void {
  const section = document.createElement("section");
  section.className = "access-forms";
  section.append(heading("Seed Docs"));

  const docs = document.createElement("section");
  docs.className = "access-form";

  for (let index = 0; index < registrationDraft.sourceDocuments.length; index += 1) {
    docs.append(registrationSourceDocumentRow(index, registrationDraft.sourceDocuments[index]));
  }

  docs.append(actionRow([
    registrationButton("Add source", "secondary-action", () => {
      registrationDraft.sourceDocuments.push({
        path: "",
        sourceContentSha256: "",
        sourceOwnerRoleId: "knowledge_steward",
        memoryTypes: ["fact"]
      });
      registrationDraftChanged(false);
      render();
    }),
    registrationStepButton("Preflight", "preflight")
  ]));

  const readiness = document.createElement("section");
  readiness.className = "access-form";
  readiness.append(
    heading("Seed readiness"),
    detailGrid(registrationSeedReadinessSummaryRows()),
    registrationValidationList(registrationSeedReadinessItems()),
    registrationSeedReadinessControls());

  section.append(docs, readiness);
  elements.detail.append(section);
}

function renderRegistrationPreflightStep(): void {
  const validation = registrationValidationItems();
  const section = document.createElement("section");
  section.className = "access-forms";
  section.append(heading("Preflight"));

  const checks = document.createElement("section");
  checks.className = "audit-section";
  checks.append(registrationValidationList(validation));

  const previewRequests = registrationPreviewRequests();
  checks.append(detailGrid([
    ["Preview candidates", previewRequests.length.toString()],
    ["Preview report", registrationDraft.accessPreviewReportId],
    ["Source docs", compactRegistrationSources().length.toString()],
    ["Hash coverage", `${registrationSourceHashCoveragePercent()}%`],
    ["Memory types", registrationCoveredMemoryTypes().join(", ")],
    ["Blocking checks", validation.filter(item => item.status === "blocked").length.toString()]
  ]));

  checks.append(actionRow([
    registrationButton("Run preview", "primary-action", () => void runRegistrationPreview()),
    registrationStepButton("Register", "register")
  ]));

  section.append(checks);
  elements.detail.append(section);
}

function renderRegistrationRegisterStep(): void {
  const section = document.createElement("section");
  section.className = "access-forms";
  section.append(heading("Register"));

  const summary = document.createElement("section");
  summary.className = "access-form";
  summary.append(
    detailGrid([
      ["Org", `${registrationDraft.organizationName} ${registrationDraft.organizationId}`.trim()],
      ["Project", `${registrationDraft.projectName} ${registrationDraft.projectId}`.trim()],
      ["Owners", compactRegistrationOwners().length.toString()],
      ["Custom roles", compactRegistrationRoles().length.toString()],
      ["Namespace grants", compactRegistrationGrants().length.toString()],
      ["Seed docs", compactRegistrationSources().length.toString()],
      ["Hash coverage", `${registrationSourceHashCoveragePercent()}%`],
      ["Source-link coverage", `${registrationSourceLinkCoveragePercent()}%`],
      ["Seed readiness", registrationSeedReadinessStatusText()],
      ["Duration", formatRegistrationDuration()],
      ["Validation failures", registrationDraft.registrationValidationFailureCount.toString()],
      ["Benchmark status", registrationSuccessBenchmarkStatusText()],
      ["Preview report", registrationDraft.accessPreviewReportId],
      ["Idempotency key", registrationDraft.idempotencyKey]
    ]),
    registrationTextarea("registration-note", "Note", registrationDraft.registrationNote, value => {
      registrationDraft.registrationNote = value;
      registrationDraftChanged(false);
    }),
    actionRow([
      registrationButton("Register", "primary-action", () => void submitRegistration()),
      registrationStepButton("Closeout", "closeout")
    ]));

  section.append(summary);
  elements.detail.append(section);
}

function renderRegistrationCloseoutStep(): void {
  const result = state.registrationResult;
  const section = document.createElement("section");
  section.className = "access-forms";
  section.append(heading("Closeout"));

  const status = document.createElement("section");
  status.className = "access-form";
  status.append(
    detailGrid([
      ["Registration", registrationSucceeded() ? readStringValue(result!, "status") : "pending"],
      ["Access-boundary review", registrationSucceeded() ? "queued" : "pending"],
      ["Context retrieval check", registrationSucceeded() ? "required" : "pending"],
      ["Feedback", registrationSucceeded() ? "required" : "pending"],
      ["Seed readiness", registrationSeedReadinessStatusText()],
      ["Role-lens readiness", registrationRoleLensReadinessLabel()],
      ["Benchmark status", registrationSuccessBenchmarkStatusText()],
      ["Payload safe", registrationSucceeded() ? readBooleanValue(result!, "payloadSafe") : ""],
      ["Raw source payloads", registrationSucceeded() ? readBooleanValue(result!, "rawSourcePayloadsIncluded") : ""]
    ]),
    registrationValidationList(registrationSeedReadinessItems()),
    actionRow([
      linkAction("Open Access", "/admin/"),
      linkAction("Open reviews", "/reviews/")
    ]));

  const benchmark = document.createElement("section");
  benchmark.className = "access-form";
  benchmark.append(
    heading("Success benchmark"),
    detailGrid(registrationSuccessBenchmarkSummaryRows()),
    registrationValidationList(registrationSuccessBenchmarkItems()),
    registrationSuccessBenchmarkControls());

  section.append(status, benchmark);
  elements.detail.append(section);
}

function registrationOwnerRow(index: number, owner: RegistrationOwnerDraft): HTMLElement {
  const row = document.createElement("div");
  row.className = "registration-row";
  row.append(
    registrationTextField(`registration-owner-principal-${index}`, "Principal ID", owner.principalId, value => {
      owner.principalId = value;
      registrationDraftChanged();
    }),
    registrationSelectField(`registration-owner-role-${index}`, "Role", owner.roleId, registrationRoleOptions(), value => {
      owner.roleId = value;
      registrationDraftChanged();
    }),
    registrationSelectField(`registration-owner-access-${index}`, "Access", owner.projectAccessLevel, ["reader", "contributor", "reviewer"], value => {
      owner.projectAccessLevel = value;
      registrationDraftChanged();
    }),
    registrationTextField(`registration-owner-label-${index}`, "Label", owner.principalLabel, value => {
      owner.principalLabel = value;
      registrationDraftChanged();
    }),
    registrationButton("Remove", "secondary-action", () => {
      registrationDraft.ownerAssignments.splice(index, 1);
      registrationDraftChanged();
      render();
    }));
  return row;
}

function registrationRoleRow(index: number, role: RegistrationRoleDefinitionDraft): HTMLElement {
  const row = document.createElement("div");
  row.className = "registration-row";
  row.append(
    registrationTextField(`registration-role-id-${index}`, "Role ID", role.roleId, value => {
      role.roleId = value;
      registrationDraftChanged();
    }),
    registrationTextField(`registration-role-display-${index}`, "Display", role.displayName, value => {
      role.displayName = value;
      registrationDraftChanged();
    }),
    registrationSelectField(`registration-role-template-${index}`, "Template", role.templateRoleId, ["", ...defaultRoleIds], value => {
      role.templateRoleId = value;
      registrationDraftChanged();
    }),
    registrationSelectField(`registration-role-status-${index}`, "Status", role.status, ["active", "disabled"], value => {
      role.status = value;
      registrationDraftChanged();
    }),
    registrationTextField(`registration-role-description-${index}`, "Description", role.description, value => {
      role.description = value;
      registrationDraftChanged();
    }),
    registrationButton("Remove", "secondary-action", () => {
      registrationDraft.roleDefinitions.splice(index, 1);
      registrationDraftChanged();
      render();
    }));
  return row;
}

function registrationGrantRow(index: number, grant: RegistrationNamespaceGrantDraft): HTMLElement {
  const row = document.createElement("div");
  row.className = "registration-row";
  row.append(
    registrationSelectField(`registration-grant-target-type-${index}`, "Target", grant.targetType, ["role", "principal"], value => {
      grant.targetType = value === "principal" ? "principal" : "role";
      registrationDraftChanged();
    }),
    registrationTextField(`registration-grant-target-id-${index}`, "Target ID", grant.targetId, value => {
      grant.targetId = value;
      registrationDraftChanged();
    }),
    registrationSelectField(`registration-grant-area-${index}`, "Area", grant.namespaceArea, registrationNamespaceAreas, value => {
      grant.namespaceArea = value;
      grant.namespacePrefix = registrationNamespaceFromArea(value);
      registrationDraftChanged();
      render();
    }),
    registrationReadOnlyField(`registration-grant-namespace-${index}`, "Namespace", grant.namespacePrefix),
    registrationSelectField(`registration-grant-permission-${index}`, "Permission", grant.permission, ["read", "write", "review"], value => {
      grant.permission = value;
      registrationDraftChanged();
    }),
    registrationButton("Remove", "secondary-action", () => {
      registrationDraft.namespaceGrants.splice(index, 1);
      registrationDraftChanged();
      render();
    }));
  return row;
}

function registrationSourceDocumentRow(index: number, source: RegistrationSourceDocumentDraft): HTMLElement {
  const row = document.createElement("div");
  row.className = "registration-row seed-doc-row";
  row.append(
    registrationTextField(`registration-source-path-${index}`, "Path", source.path, value => {
      source.path = value;
      registrationDraftChanged(false);
    }),
    registrationTextField(`registration-source-hash-${index}`, "SHA-256", source.sourceContentSha256, value => {
      source.sourceContentSha256 = value.toLowerCase();
      registrationDraftChanged(false);
    }),
    registrationSelectField(`registration-source-owner-${index}`, "Owner role", source.sourceOwnerRoleId, registrationRoleOptions(), value => {
      source.sourceOwnerRoleId = value;
      registrationDraftChanged(false);
    }),
    registrationMemoryTypeChecklist(`registration-source-memory-types-${index}`, source.memoryTypes, values => {
      source.memoryTypes = values;
      registrationDraftChanged(false);
    }),
    registrationButton("Remove", "secondary-action", () => {
      registrationDraft.sourceDocuments.splice(index, 1);
      registrationDraftChanged(false);
      render();
    }));
  return row;
}

function registrationSection(title: string, fields: HTMLElement[], actions: HTMLElement[]): HTMLElement {
  const section = document.createElement("section");
  section.className = "access-form";
  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(...fields);
  section.append(heading(title), grid, actionRow(actions));
  return section;
}

function registrationTextField(
  id: string,
  labelText: string,
  value: string,
  onInput: (value: string) => void): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const input = document.createElement("input");
  span.textContent = labelText;
  input.id = id;
  input.value = value;
  input.autocomplete = "off";
  input.spellcheck = false;
  input.addEventListener("input", () => onInput(input.value.trim()));
  label.append(span, input);
  return label;
}

function registrationTextarea(
  id: string,
  labelText: string,
  value: string,
  onInput: (value: string) => void): HTMLElement {
  const label = document.createElement("label");
  label.className = "registration-wide-field";
  const span = document.createElement("span");
  const textarea = document.createElement("textarea");
  span.textContent = labelText;
  textarea.id = id;
  textarea.value = value;
  textarea.rows = 3;
  textarea.addEventListener("input", () => onInput(textarea.value.trim()));
  label.append(span, textarea);
  return label;
}

function registrationReadOnlyField(
  id: string,
  labelText: string,
  value: string): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const input = document.createElement("input");
  span.textContent = labelText;
  input.id = id;
  input.value = value;
  input.readOnly = true;
  input.tabIndex = -1;
  label.append(span, input);
  return label;
}

function registrationSelectField(
  id: string,
  labelText: string,
  value: string,
  values: string[],
  onChange: (value: string) => void): HTMLElement {
  const label = document.createElement("label");
  const span = document.createElement("span");
  const select = document.createElement("select");
  span.textContent = labelText;
  select.id = id;

  for (const optionValue of values) {
    const option = document.createElement("option");
    option.value = optionValue;
    option.textContent = optionValue;
    option.selected = optionValue === value;
    select.append(option);
  }

  select.addEventListener("change", () => onChange(select.value));
  label.append(span, select);
  return label;
}

function registrationMemoryTypeChecklist(
  idPrefix: string,
  selectedValues: string[],
  onChange: (values: string[]) => void): HTMLElement {
  const fieldset = document.createElement("fieldset");
  fieldset.className = "registration-check-group";
  const legend = document.createElement("legend");
  legend.textContent = "Memory types";
  fieldset.append(legend);

  for (const memoryType of registrationMemoryTypes) {
    const label = document.createElement("label");
    const input = document.createElement("input");
    const span = document.createElement("span");
    input.id = `${idPrefix}-${memoryType}`;
    input.type = "checkbox";
    input.checked = selectedValues.includes(memoryType);
    span.textContent = memoryType;
    input.addEventListener("change", () => {
      const next = new Set(selectedValues);
      if (input.checked) {
        next.add(memoryType);
      } else {
        next.delete(memoryType);
      }

      onChange(registrationMemoryTypes.filter(type => next.has(type)));
      render();
    });
    label.append(input, span);
    fieldset.append(label);
  }

  return fieldset;
}

function registrationButton(label: string, className: string, onClick: () => void): HTMLButtonElement {
  const button = document.createElement("button");
  button.type = "button";
  button.className = className;
  button.textContent = label;
  button.disabled = state.busy;
  button.addEventListener("click", onClick);
  return button;
}

function registrationStepButton(label: string, stepId: RegistrationStepId): HTMLButtonElement {
  return registrationButton(label, "secondary-action", () => {
    state.selectedRegistrationStepId = stepId;
    render();
  });
}

function registrationValidationList(items: RegistrationValidationItem[]): HTMLElement {
  const list = document.createElement("div");
  list.className = "audit-list";

  for (const item of items) {
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

function registrationSeedReadinessControls(): HTMLElement {
  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(
    registrationSelectField("registration-seed-context-check", "Context check", registrationDraft.seedContextCheckStatus, ["pending", "passed", "failed"], value => {
      registrationDraft.seedContextCheckStatus = value === "passed" || value === "failed" ? value : "pending";
      registrationDraftChanged(false);
      render();
    }),
    registrationSelectField("registration-seed-feedback", "Feedback", registrationDraft.seedFeedbackStatus, ["pending", "recorded"], value => {
      registrationDraft.seedFeedbackStatus = value === "recorded" ? "recorded" : "pending";
      registrationDraftChanged(false);
      render();
    }));
  return grid;
}

function registrationSeedReadinessItems(): RegistrationValidationItem[] {
  const sources = compactRegistrationSources();
  const ownerRoles = registrationSourceOwnerRoles();
  const coveredMemoryTypes = registrationCoveredMemoryTypes();
  const missingRoleLensRoles = registrationMissingRoleLensRoles();
  const sourceCount = sources.length;

  return [
    {
      id: "source_doc_paths",
      label: "Source document paths",
      status: sourceCount > 0 && sources.every(source => source.path) ? "done" : "blocked",
      detail: sourceCount === 0 ? "at least one source document required" : `${sourceCount} source document${sourceCount === 1 ? "" : "s"}`
    },
    {
      id: "source_hash_status",
      label: "Source hash coverage",
      status: sourceCount > 0 && sources.every(source => sha256IsValid(source.sourceContentSha256)) ? "done" : "blocked",
      detail: `${registrationSourceHashCoveragePercent()}% valid SHA-256 coverage`
    },
    {
      id: "source_owner_status",
      label: "Source owners",
      status: sourceCount > 0 && sources.every(source => roleIsKnown(source.sourceOwnerRoleId)) ? "done" : "blocked",
      detail: ownerRoles.length === 0 ? "source owner role required" : ownerRoles.join(", ")
    },
    {
      id: "memory_type_coverage",
      label: "Memory type coverage",
      status: sourceCount > 0
        && sources.every(source => source.memoryTypes.length > 0 && source.memoryTypes.every(registrationMemoryTypeIsCanonical))
        ? "done"
        : "blocked",
      detail: coveredMemoryTypes.length === 0 ? "canonical memory type required" : coveredMemoryTypes.join(", ")
    },
    {
      id: "role_lens_readiness",
      label: "Role-lens readiness",
      status: missingRoleLensRoles.length === 0 && registrationRoleLensOwnerRoles().length > 0 ? "done" : "needs_review",
      detail: registrationRoleLensReadinessLabel()
    },
    {
      id: "context_checks",
      label: "Context checks",
      status: registrationDraft.seedContextCheckStatus === "passed"
        ? "done"
        : registrationDraft.seedContextCheckStatus === "failed" ? "blocked" : "needs_review",
      detail: registrationDraft.seedContextCheckStatus
    },
    {
      id: "feedback_closeout",
      label: "Feedback closeout",
      status: registrationDraft.seedFeedbackStatus === "recorded" ? "done" : "needs_review",
      detail: registrationDraft.seedFeedbackStatus
    }
  ];
}

function registrationSeedReadinessSummaryRows(): [string, string][] {
  const missingRoleLensRoles = registrationMissingRoleLensRoles();

  return [
    ["Source docs", compactRegistrationSources().length.toString()],
    ["Hash coverage", `${registrationSourceHashCoveragePercent()}%`],
    ["Source owners", registrationSourceOwnerRoles().join(", ")],
    ["Memory types", registrationCoveredMemoryTypes().join(", ")],
    ["Role-lens missing", missingRoleLensRoles.length === 0 ? "none" : missingRoleLensRoles.join(", ")],
    ["Context check", registrationDraft.seedContextCheckStatus],
    ["Feedback", registrationDraft.seedFeedbackStatus],
    ["Raw source payloads", "not included"]
  ];
}

function registrationSeedReadinessEvidence(): Record<string, unknown> {
  return {
    payloadSafe: true,
    rawSourcePayloadsIncluded: false,
    sourceDocumentCount: compactRegistrationSources().length,
    sourceHashCoveragePercent: registrationSourceHashCoveragePercent(),
    sourceOwnerRoles: registrationSourceOwnerRoles(),
    memoryTypes: registrationCoveredMemoryTypes(),
    roleLensOwnerRoles: registrationRoleLensOwnerRoles(),
    roleLensMissingRoles: registrationMissingRoleLensRoles(),
    contextCheckStatus: registrationDraft.seedContextCheckStatus,
    feedbackStatus: registrationDraft.seedFeedbackStatus,
    status: registrationSeedReadinessStatusText()
  };
}

function registrationSuccessBenchmarkControls(): HTMLElement {
  const grid = document.createElement("div");
  grid.className = "access-grid";
  grid.append(
    registrationReadOnlyField("registration-started-at", "Started", registrationDraft.registrationStartedAt || "pending"),
    registrationReadOnlyField("registration-completed-at", "Completed", registrationDraft.registrationCompletedAt || "pending"),
    registrationSelectField("registration-access-drift", "Access drift", registrationDraft.accessDriftStatus, ["pending", "clear", "drift_found"], value => {
      registrationDraft.accessDriftStatus = value === "clear" || value === "drift_found" ? value : "pending";
      render();
    }),
    registrationSelectField("registration-confidence-score", "Confidence", registrationDraft.userConfidenceScore, ["", "1", "2", "3", "4", "5"], value => {
      registrationDraft.userConfidenceScore = ["1", "2", "3", "4", "5"].includes(value) ? value : "";
      render();
    }));
  return grid;
}

function registrationSuccessBenchmarkItems(): RegistrationValidationItem[] {
  const sourceLinkCoverage = registrationSourceLinkCoveragePercent();
  const confidenceScore = registrationUserConfidenceScoreValue();

  return [
    {
      id: "registration_duration",
      label: "Registration duration",
      status: registrationSucceeded() ? "done" : "needs_review",
      detail: registrationSucceeded()
        ? `${formatRegistrationDuration()} (${registrationDurationSeconds()} seconds)`
        : `timer running from ${registrationDraft.registrationStartedAt || "pending"}`
    },
    {
      id: "registration_validation_failures",
      label: "Validation failures",
      status: registrationDraft.registrationValidationFailureCount === 0 ? "done" : "needs_review",
      detail: `${registrationDraft.registrationValidationFailureCount} blocked submit attempt${registrationDraft.registrationValidationFailureCount === 1 ? "" : "s"}`
    },
    {
      id: "registration_access_drift",
      label: "Access drift",
      status: registrationDraft.accessDriftStatus === "clear"
        ? "done"
        : registrationDraft.accessDriftStatus === "drift_found" ? "blocked" : "needs_review",
      detail: registrationDraft.accessDriftStatus
    },
    {
      id: "registration_source_link_coverage",
      label: "Source-link coverage",
      status: sourceLinkCoverage === 100 ? "done" : "blocked",
      detail: `${sourceLinkCoverage}% path/hash/owner coverage`
    },
    {
      id: "registration_user_confidence",
      label: "User confidence",
      status: confidenceScore >= 4 ? "done" : "needs_review",
      detail: registrationUserConfidenceLabel()
    }
  ];
}

function registrationSuccessBenchmarkSummaryRows(): [string, string][] {
  return [
    ["Started", registrationDraft.registrationStartedAt || "pending"],
    ["Completed", registrationDraft.registrationCompletedAt || "pending"],
    ["Duration", formatRegistrationDuration()],
    ["Validation failures", registrationDraft.registrationValidationFailureCount.toString()],
    ["Current blocking checks", registrationCurrentBlockingValidationCount().toString()],
    ["Access drift", registrationDraft.accessDriftStatus],
    ["Source-link coverage", `${registrationSourceLinkCoveragePercent()}%`],
    ["User confidence", registrationUserConfidenceLabel()],
    ["Project-success evidence", registrationSuccessBenchmarkStatusText()],
    ["Raw source payloads", "not included"]
  ];
}

function registrationSuccessBenchmarkEvidence(): Record<string, unknown> {
  return {
    benchmarkId: "REG-06",
    payloadSafe: true,
    rawSourcePayloadsIncluded: false,
    projectSuccessEvidenceLoop: true,
    registrationStartedAt: registrationDraft.registrationStartedAt || null,
    registrationCompletedAt: registrationDraft.registrationCompletedAt || null,
    registrationDurationSeconds: registrationDurationSeconds(),
    validationFailureCount: registrationDraft.registrationValidationFailureCount,
    currentBlockingValidationCount: registrationCurrentBlockingValidationCount(),
    accessDriftFindingCount: registrationAccessDriftFindingCount(),
    accessDriftStatus: registrationDraft.accessDriftStatus,
    sourceLinkCoverage: registrationSourceLinkCoverageRatio(),
    sourceLinkCoveragePercent: registrationSourceLinkCoveragePercent(),
    sourceHashCoveragePercent: registrationSourceHashCoveragePercent(),
    userConfidence: registrationUserConfidenceScoreValue() || null,
    userConfidenceScore: registrationUserConfidenceScoreValue() || null,
    projectRegistration: {
      registrationDurationSeconds: registrationDurationSeconds(),
      validationFailureCount: registrationDraft.registrationValidationFailureCount,
      accessDriftFindingCount: registrationAccessDriftFindingCount(),
      sourceLinkCoverage: registrationSourceLinkCoverageRatio(),
      userConfidence: registrationUserConfidenceScoreValue() || null
    },
    checks: registrationSuccessBenchmarkItems(),
    status: registrationSuccessBenchmarkStatusText()
  };
}

function registrationValidationItems(): RegistrationValidationItem[] {
  const items: RegistrationValidationItem[] = [];
  const projectPrefix = registrationProjectPrefix();
  const owners = compactRegistrationOwners();
  const roles = compactRegistrationRoles();
  const grants = compactRegistrationGrants();
  const customRoleIds = new Set(roles.filter(role => role.status !== "disabled").map(role => role.roleId));
  const previewIsCurrent = registrationDraft.accessPreviewReportId
    && registrationPreviewFingerprint === registrationAccessPlanFingerprint();

  items.push({
    id: "scope_ids",
    label: "Scope IDs",
    status: isGuid(registrationDraft.organizationId) && isGuid(registrationDraft.projectId) ? "done" : "blocked",
    detail: "organizationId and projectId must be UUIDs"
  });
  items.push({
    id: "scope_names",
    label: "Scope names",
    status: registrationDraft.organizationName && registrationDraft.projectName ? "done" : "blocked",
    detail: "organizationName and projectName are required"
  });
  items.push({
    id: "owners_present",
    label: "Owner assignments",
    status: owners.length > 0
      && owners.every(owner => isGuid(owner.principalId) && roleIsKnown(owner.roleId))
      && registrationMissingRequiredOwnerRoles(owners).length === 0
      ? "done"
      : "blocked",
    detail: registrationOwnerAssignmentDetail(owners)
  });
  items.push({
    id: "roles_active",
    label: "Custom role definitions",
    status: roles.every(role => role.roleId && role.displayName && roleIdentifierIsValid(role.roleId)) ? "done" : "blocked",
    detail: roles.length === 0 ? "default role templates only" : `${roles.length} custom role definition${roles.length === 1 ? "" : "s"}`
  });
  items.push({
    id: "grants_safe",
    label: "Namespace grants",
    status: grants.length > 0
      && grants.every(grant => registrationGrantIsSafe(grant, projectPrefix, customRoleIds))
      ? "done"
      : "blocked",
    detail: "read/write/review grants must stay below the project namespace"
  });
  items.push(...registrationSeedReadinessItems());
  items.push({
    id: "access_preview",
    label: "Access preview",
    status: previewIsCurrent ? "done" : "blocked",
    detail: registrationDraft.accessPreviewReportId
      ? "preview is stale after access-plan edits"
      : "run preview before registration"
  });
  items.push({
    id: "closeout",
    label: "Closeout evidence",
    status: registrationSucceeded() ? "done" : "needs_review",
    detail: registrationSucceeded() ? "audit evidence captured" : "registration result pending"
  });

  return items;
}

function registrationStepStatus(stepId: RegistrationStepId): RegistrationValidationStatus {
  if (stepId === "scope") {
    return registrationValidationItems().some(item => item.id.startsWith("scope_") && item.status === "blocked") ? "blocked" : "done";
  }

  if (stepId === "owners_roles") {
    return registrationValidationItems().some(item => (item.id === "owners_present" || item.id === "roles_active") && item.status === "blocked") ? "blocked" : "done";
  }

  if (stepId === "grants") {
    return registrationValidationItems().find(item => item.id === "grants_safe")?.status ?? "blocked";
  }

  if (stepId === "seed_docs") {
    const seedItems = registrationSeedReadinessItems();
    if (seedItems.some(item => item.status === "blocked")) {
      return "blocked";
    }

    return seedItems.some(item => item.status === "needs_review") ? "needs_review" : "done";
  }

  if (stepId === "preflight") {
    return registrationValidationItems().find(item => item.id === "access_preview")?.status ?? "blocked";
  }

  if (stepId === "register") {
    return registrationSucceeded() ? "done" : "needs_review";
  }

  return registrationSucceeded() ? "done" : "needs_review";
}

function registrationStepSummary(stepId: RegistrationStepId): string {
  if (stepId === "scope") {
    return registrationDraft.projectName || registrationDraft.projectId || "Project boundary";
  }

  if (stepId === "owners_roles") {
    return `${compactRegistrationOwners().length} owners, ${compactRegistrationRoles().length} custom roles`;
  }

  if (stepId === "grants") {
    return `${compactRegistrationGrants().length} namespace grants`;
  }

  if (stepId === "seed_docs") {
    return `${compactRegistrationSources().length} source docs, ${registrationSourceHashCoveragePercent()}% hash`;
  }

  if (stepId === "preflight") {
    return registrationDraft.accessPreviewReportId || "Preview required";
  }

  if (stepId === "register") {
    return registrationSucceeded() ? readStringValue(state.registrationResult!, "status") : "Not submitted";
  }

  return registrationSucceeded() ? "Closeout required" : "Waiting for registration";
}

async function runRegistrationPreview(): Promise<void> {
  const requests = registrationPreviewRequests();
  if (requests.length === 0) {
    registrationPreviewResults = [{ error: "No preview candidates. Add owner assignments and grants first." }];
    setStatus("Preview blocked");
    render();
    return;
  }

  setBusy(true);
  setStatus("Previewing");

  try {
    const results: Record<string, unknown>[] = [];
    for (const request of requests) {
      const result = await apiFetch<Record<string, unknown>>("/api/admin/access/effective-preview", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(request)
      });
      results.push(result);
    }

    registrationPreviewResults = results;
    registrationDraft.accessPreviewReportId = `admin-wizard-preview-${new Date().toISOString().replaceAll(":", "").replaceAll(".", "")}`;
    registrationPreviewFingerprint = registrationAccessPlanFingerprint();
    setStatus(`${results.length} previews`);
  } catch (error) {
    registrationPreviewResults = [{ error: errorMessage(error) }];
    setStatus("Preview error");
  } finally {
    setBusy(false);
    render();
  }
}

async function submitRegistration(): Promise<void> {
  syncRegistrationGrantNamespaces();
  const blocking = registrationValidationItems().filter(item => item.status === "blocked");
  if (blocking.length > 0) {
    registrationDraft.registrationValidationFailureCount += 1;
    registrationPreviewResults = [{
      error: "Registration blocked by preflight",
      blocking
    }];
    setStatus("Registration blocked");
    render();
    return;
  }

  setBusy(true);
  setStatus("Registering");

  try {
    state.registrationResult = await apiFetch<Record<string, unknown>>("/api/admin/projects/register", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Idempotency-Key": registrationDraft.idempotencyKey
      },
      body: JSON.stringify(registrationRequestBody())
    });
    registrationDraft.registrationCompletedAt = new Date().toISOString();
    state.selectedRegistrationStepId = "closeout";
    setStatus("Registered");
  } catch (error) {
    state.registrationResult = { error: errorMessage(error) };
    setStatus("Registration error");
  } finally {
    setBusy(false);
    render();
  }
}

function registrationPreviewRequests(): RegistrationPreviewRequest[] {
  const requests: RegistrationPreviewRequest[] = [];
  const seen = new Set<string>();
  const owners = compactRegistrationOwners();

  if (!isGuid(registrationDraft.projectId)) {
    return requests;
  }

  for (const grant of compactRegistrationGrants()) {
    const principalIds = grant.targetType === "principal"
      ? [grant.targetId]
      : owners.filter(owner => owner.roleId === grant.targetId).map(owner => owner.principalId);

    for (const principalId of principalIds) {
      if (!isGuid(principalId)) {
        continue;
      }

      const request: RegistrationPreviewRequest = {
        principalId,
        permission: grant.permission,
        scopeType: "project",
        scopeId: registrationDraft.projectId,
        namespacePrefix: grant.namespacePrefix
      };
      const key = `${request.principalId}:${request.permission}:${request.namespacePrefix}`;
      if (!seen.has(key)) {
        seen.add(key);
        requests.push(request);
      }
    }
  }

  return requests;
}

function registrationRequestBody(): Record<string, unknown> {
  return {
    organizationId: registrationDraft.organizationId,
    organizationName: registrationDraft.organizationName,
    projectId: registrationDraft.projectId,
    projectName: registrationDraft.projectName,
    projectStatus: registrationDraft.projectStatus,
    roleDefinitions: compactRegistrationRoles(),
    ownerAssignments: compactRegistrationOwners(),
    namespaceGrants: compactRegistrationGrants().map(grant => ({
      principalId: grant.targetType === "principal" ? grant.targetId : null,
      roleId: grant.targetType === "role" ? grant.targetId : null,
      namespacePrefix: grant.namespacePrefix,
      permission: grant.permission
    })),
    sourceDocuments: compactRegistrationSources().map(source => ({
      path: source.path,
      sourceContentSha256: source.sourceContentSha256,
      sourceOwnerRoleId: source.sourceOwnerRoleId
    })),
    accessPreviewReportId: registrationDraft.accessPreviewReportId,
    auditExportId: registrationDraft.auditExportId || null,
    registrationNote: registrationDraft.registrationNote || null
  };
}

function compactRegistrationOwners(): RegistrationOwnerDraft[] {
  return registrationDraft.ownerAssignments
    .map(owner => ({
      principalId: owner.principalId.trim(),
      roleId: normalizeRegistrationText(owner.roleId),
      projectAccessLevel: normalizeRegistrationText(owner.projectAccessLevel),
      principalLabel: owner.principalLabel.trim()
    }))
    .filter(owner => owner.principalId || owner.roleId || owner.principalLabel);
}

function compactRegistrationRoles(): RegistrationRoleDefinitionDraft[] {
  return registrationDraft.roleDefinitions
    .map(role => ({
      roleId: normalizeRegistrationText(role.roleId),
      displayName: role.displayName.trim(),
      description: role.description.trim(),
      templateRoleId: normalizeRegistrationText(role.templateRoleId),
      status: normalizeRegistrationText(role.status || "active")
    }))
    .filter(role => role.roleId || role.displayName);
}

function compactRegistrationGrants(): RegistrationNamespaceGrantDraft[] {
  syncRegistrationGrantNamespaces();

  return registrationDraft.namespaceGrants
    .map(grant => ({
      targetType: grant.targetType,
      targetId: normalizeRegistrationText(grant.targetId),
      namespaceArea: grant.namespaceArea,
      namespacePrefix: grant.namespacePrefix.trim().replace(/\/$/, ""),
      permission: normalizeRegistrationText(grant.permission)
    }))
    .filter(grant => grant.targetId || grant.namespacePrefix);
}

function compactRegistrationSources(): RegistrationSourceDocumentDraft[] {
  return registrationDraft.sourceDocuments
    .map(source => ({
      path: source.path.trim(),
      sourceContentSha256: source.sourceContentSha256.trim().toLowerCase(),
      sourceOwnerRoleId: normalizeRegistrationText(source.sourceOwnerRoleId),
      memoryTypes: source.memoryTypes
        .map(normalizeRegistrationText)
        .filter(registrationMemoryTypeIsCanonical)
    }))
    .filter(source => source.path || source.sourceContentSha256 || source.memoryTypes.length > 0);
}

function registrationSourceHashCoveragePercent(): number {
  const sources = compactRegistrationSources();
  if (sources.length === 0) {
    return 0;
  }

  return Math.round(100 * sources.filter(source => sha256IsValid(source.sourceContentSha256)).length / sources.length);
}

function registrationSourceLinkCoveragePercent(): number {
  const sources = compactRegistrationSources();
  if (sources.length === 0) {
    return 0;
  }

  return Math.round(100 * sources.filter(source =>
    source.path
    && sha256IsValid(source.sourceContentSha256)
    && roleIsKnown(source.sourceOwnerRoleId)).length / sources.length);
}

function registrationSourceLinkCoverageRatio(): number {
  return registrationSourceLinkCoveragePercent() / 100;
}

function registrationSourceOwnerRoles(): string[] {
  return uniqueSorted(compactRegistrationSources().map(source => source.sourceOwnerRoleId).filter(roleIsKnown));
}

function registrationCoveredMemoryTypes(): string[] {
  return uniqueSorted(compactRegistrationSources().flatMap(source => source.memoryTypes));
}

function registrationRoleLensOwnerRoles(): string[] {
  return uniqueSorted(compactRegistrationSources()
    .filter(source => source.memoryTypes.includes("role_lens") && roleIsKnown(source.sourceOwnerRoleId))
    .map(source => source.sourceOwnerRoleId));
}

function registrationRequiredRoleLensRoles(): string[] {
  return uniqueSorted(compactRegistrationOwners()
    .map(owner => owner.roleId)
    .filter(roleId => roleIsKnown(roleId)));
}

function registrationMissingRoleLensRoles(): string[] {
  const coveredRoles = new Set(registrationRoleLensOwnerRoles());
  return registrationRequiredRoleLensRoles().filter(roleId => !coveredRoles.has(roleId));
}

function registrationRoleLensReadinessLabel(): string {
  const covered = registrationRoleLensOwnerRoles();
  const missing = registrationMissingRoleLensRoles();
  if (covered.length === 0 && missing.length === 0) {
    return "pending";
  }

  if (missing.length === 0) {
    return `covered: ${covered.join(", ")}`;
  }

  return `missing: ${missing.join(", ")}`;
}

function registrationSeedReadinessStatusText(): string {
  const items = registrationSeedReadinessItems();
  if (items.some(item => item.status === "blocked")) {
    return "blocked";
  }

  return items.some(item => item.status === "needs_review") ? "needs_review" : "done";
}

function registrationSuccessBenchmarkStatusText(): string {
  const items = registrationSuccessBenchmarkItems();
  if (items.some(item => item.status === "blocked")) {
    return "blocked";
  }

  return items.some(item => item.status === "needs_review") ? "needs_review" : "done";
}

function registrationCurrentBlockingValidationCount(): number {
  return registrationValidationItems().filter(item => item.status === "blocked").length;
}

function registrationAccessDriftFindingCount(): number | null {
  if (registrationDraft.accessDriftStatus === "pending") {
    return null;
  }

  return registrationDraft.accessDriftStatus === "drift_found" ? 1 : 0;
}

function registrationDurationSeconds(): number {
  const startedAt = Date.parse(registrationDraft.registrationStartedAt);
  const completedAt = Date.parse(registrationDraft.registrationCompletedAt || new Date().toISOString());
  if (!Number.isFinite(startedAt) || !Number.isFinite(completedAt) || completedAt < startedAt) {
    return 0;
  }

  return Math.round((completedAt - startedAt) / 1000);
}

function formatRegistrationDuration(): string {
  const seconds = registrationDurationSeconds();
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

function registrationUserConfidenceScoreValue(): number {
  const score = Number.parseInt(registrationDraft.userConfidenceScore, 10);
  return Number.isInteger(score) && score >= 1 && score <= 5 ? score : 0;
}

function registrationUserConfidenceLabel(): string {
  const score = registrationUserConfidenceScoreValue();
  return score === 0 ? "pending" : `${score}/5`;
}

function registrationMemoryTypeIsCanonical(value: string): boolean {
  return registrationMemoryTypes.includes(normalizeRegistrationText(value));
}

function uniqueSorted(values: string[]): string[] {
  return [...new Set(values.filter(value => value))].sort((left, right) => left.localeCompare(right));
}

function registrationGrantIsSafe(
  grant: RegistrationNamespaceGrantDraft,
  projectPrefix: string,
  customRoleIds: Set<string>): boolean {
  if (!grant.targetId || !grant.namespacePrefix || !["read", "write", "review"].includes(grant.permission)) {
    return false;
  }

  if (grant.targetType === "principal" && !isGuid(grant.targetId)) {
    return false;
  }

  if (grant.targetType === "role" && !roleIsKnown(grant.targetId) && !customRoleIds.has(grant.targetId)) {
    return false;
  }

  return Boolean(projectPrefix)
    && grant.namespacePrefix.startsWith(`${projectPrefix}/`)
    && grant.namespacePrefix !== projectPrefix
    && !["/project", "/org", "/global", "/user", "/role", "/agent", "/session"].includes(grant.namespacePrefix);
}

function syncRegistrationGrantNamespaces(): void {
  for (const grant of registrationDraft.namespaceGrants) {
    grant.namespacePrefix = registrationNamespaceFromArea(grant.namespaceArea);
  }
}

function registrationNamespaceFromArea(area: string): string {
  const projectPrefix = registrationProjectPrefix();
  if (!projectPrefix) {
    return "";
  }

  return `${projectPrefix}/${area}`;
}

function registrationProjectPrefix(): string {
  return isGuid(registrationDraft.projectId)
    ? `/project/${registrationDraft.projectId.toLowerCase()}`
    : "";
}

function registrationRoleOptions(): string[] {
  const customRoleIds = compactRegistrationRoles().map(role => role.roleId).filter(roleId => roleId);
  return [...defaultRoleIds, ...customRoleIds];
}

function registrationMissingRequiredOwnerRoles(owners = compactRegistrationOwners()): string[] {
  const ownerRoleIds = new Set(owners.map(owner => owner.roleId));
  return registrationRequiredOwnerRoleIds.filter(roleId => !ownerRoleIds.has(roleId));
}

function registrationOwnerAssignmentDetail(owners: RegistrationOwnerDraft[]): string {
  if (owners.length === 0) {
    return "Product Owner, Knowledge Steward, and Security/Ops owner principals are required";
  }

  if (owners.some(owner => !isGuid(owner.principalId) || !roleIsKnown(owner.roleId))) {
    return "owner principals must be UUIDs and owner roles must be active or known";
  }

  const missingRequiredRoles = registrationMissingRequiredOwnerRoles(owners);
  if (missingRequiredRoles.length > 0) {
    return `missing required owner roles: ${missingRequiredRoles.join(", ")}`;
  }

  return "Product Owner, Knowledge Steward, and Security/Ops owner principals are required";
}

function roleIsKnown(roleId: string): boolean {
  return defaultRoleIds.includes(roleId) || compactRegistrationRoles().some(role => role.roleId === roleId && role.status === "active");
}

function roleIdentifierIsValid(value: string): boolean {
  return /^[a-z][a-z0-9_-]{0,63}$/.test(value);
}

function sha256IsValid(value: string): boolean {
  return /^[a-f0-9]{64}$/.test(value.trim().toLowerCase());
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value.trim());
}

function normalizeRegistrationText(value: string): string {
  return value.trim().toLowerCase();
}

function randomRegistrationId(): string {
  if (crypto.randomUUID) {
    return crypto.randomUUID();
  }

  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function registrationDraftChanged(previewRelevant = true): void {
  state.registrationResult = null;
  registrationDraft.registrationCompletedAt = "";
  registrationDraft.accessDriftStatus = "pending";

  if (previewRelevant) {
    registrationDraft.accessPreviewReportId = "";
    registrationPreviewResults = [];
    registrationPreviewFingerprint = "";
  }
}

function registrationAccessPlanFingerprint(): string {
  return JSON.stringify({
    projectId: registrationDraft.projectId.trim().toLowerCase(),
    roleDefinitions: compactRegistrationRoles(),
    ownerAssignments: compactRegistrationOwners(),
    namespaceGrants: compactRegistrationGrants()
  });
}

function registrationSucceeded(): boolean {
  return state.registrationResult !== null
    && typeof state.registrationResult.error !== "string"
    && readStringValue(state.registrationResult, "status") === "registered";
}

function readStringValue(value: Record<string, unknown>, key: string): string {
  const item = value[key];
  return typeof item === "string" ? item : "";
}

function readBooleanValue(value: Record<string, unknown>, key: string): string {
  const item = value[key];
  return typeof item === "boolean" ? (item ? "yes" : "no") : "";
}

function readNumberValue(value: Record<string, unknown>, key: string): number {
  const item = value[key];
  return typeof item === "number" ? item : 0;
}
