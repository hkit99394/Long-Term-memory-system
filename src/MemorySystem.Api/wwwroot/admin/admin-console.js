// Generated from tools/ui/src/admin/access-panel.ts, tools/ui/src/admin/registration-panel.ts, tools/ui/src/admin/management-panel.ts, tools/ui/src/admin-console.ts. Run npm run build in tools/ui.
"use strict";
const defaultRoleIds = [
    "product_owner",
    "cto",
    "security_professional",
    "it_manager",
    "developer",
    "tester_qa",
    "release_manager",
    "knowledge_steward",
    "designer",
    "cfo",
    "coo",
    "ceo"
];
function renderAccessDetail() {
    const panel = document.createElement("section");
    panel.className = "access-forms";
    panel.append(heading("Access management"), accessForm("Organization membership", [
        textField("orgId", "Org ID"),
        textField("principalId", "Principal ID"),
        selectField("accessLevel", "Access", ["reader", "contributor", "reviewer", "admin", "owner"])
    ], "Save", form => postAccess("/api/admin/access/organization-memberships", {
        orgId: formValue(form, "orgId"),
        principalId: formValue(form, "principalId"),
        accessLevel: formValue(form, "accessLevel")
    })), accessForm("Project membership", [
        textField("projectId", "Project ID"),
        textField("principalId", "Principal ID"),
        selectField("accessLevel", "Access", ["reader", "contributor", "reviewer", "admin"])
    ], "Save", form => postAccess("/api/admin/access/project-memberships", {
        projectId: formValue(form, "projectId"),
        principalId: formValue(form, "principalId"),
        accessLevel: formValue(form, "accessLevel")
    })), accessForm("Project role definition", [
        textField("projectId", "Project ID"),
        textField("roleId", "Role ID"),
        textField("displayName", "Display name"),
        textField("description", "Description"),
        selectField("templateRoleId", "Template role", ["", ...defaultRoleIds]),
        selectField("status", "Status", ["active", "disabled"])
    ], "Save", form => {
        const templateRoleId = formValue(form, "templateRoleId");
        const description = formValue(form, "description");
        return postAccess("/api/admin/access/project-roles", {
            projectId: formValue(form, "projectId"),
            roleId: formValue(form, "roleId"),
            displayName: formValue(form, "displayName"),
            description: description || null,
            templateRoleId: templateRoleId || null,
            status: formValue(form, "status")
        });
    }), accessForm("Role assignment", [
        textField("principalId", "Principal ID"),
        textField("roleId", "Role ID"),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID")
    ], "Assign", form => postAccess("/api/admin/access/role-assignments", {
        principalId: formValue(form, "principalId"),
        roleId: formValue(form, "roleId"),
        scopeType: formValue(form, "scopeType"),
        scopeId: formValue(form, "scopeId")
    })), accessForm("Namespace grant", [
        selectField("targetType", "Target", ["principal", "role"]),
        textField("targetId", "Target ID"),
        textField("namespacePrefix", "Namespace"),
        selectField("permission", "Permission", ["read", "write", "review"]),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID")
    ], "Grant", form => {
        const targetType = formValue(form, "targetType");
        return postAccess("/api/admin/access/namespace-grants", {
            principalId: targetType === "principal" ? formValue(form, "targetId") : null,
            roleId: targetType === "role" ? formValue(form, "targetId") : null,
            namespacePrefix: formValue(form, "namespacePrefix"),
            permission: formValue(form, "permission"),
            scopeType: formValue(form, "scopeType"),
            scopeId: formValue(form, "scopeId")
        });
    }), accessForm("Break-glass namespace admin", [
        textField("principalId", "Principal ID"),
        textField("namespacePrefix", "Namespace"),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID"),
        selectField("ownerRole", "Owner role", defaultRoleIds),
        selectField("acceptedByRole", "Accepted by role", defaultRoleIds),
        textField("reason", "Reason"),
        dateField("reviewDue", "Review due"),
        textField("cleanupAction", "Cleanup action"),
        textField("auditEvidenceId", "Audit evidence ID")
    ], "Grant", form => postAccess("/api/admin/access/namespace-grants", {
        principalId: formValue(form, "principalId"),
        roleId: null,
        namespacePrefix: formValue(form, "namespacePrefix"),
        permission: "admin",
        scopeType: formValue(form, "scopeType"),
        scopeId: formValue(form, "scopeId"),
        breakGlassEvidence: {
            ownerRole: formValue(form, "ownerRole"),
            acceptedByRole: formValue(form, "acceptedByRole"),
            reason: formValue(form, "reason"),
            reviewDue: formValue(form, "reviewDue"),
            cleanupAction: formValue(form, "cleanupAction"),
            auditEvidenceId: formValue(form, "auditEvidenceId")
        }
    })), accessForm("Effective preview", [
        textField("principalId", "Principal ID"),
        selectField("permission", "Permission", ["read", "write", "review", "admin"]),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID"),
        textField("namespacePrefix", "Namespace")
    ], "Preview", form => postAccess("/api/admin/access/effective-preview", {
        principalId: formValue(form, "principalId"),
        permission: formValue(form, "permission"),
        scopeType: formValue(form, "scopeType"),
        scopeId: formValue(form, "scopeId"),
        namespacePrefix: formValue(form, "namespacePrefix") || null
    })), accessForm("Audit export", [
        dateTimeField("occurredFrom", "From"),
        dateTimeField("occurredTo", "To"),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID"),
        selectField("actionType", "Action", [
            "all",
            "authentication",
            "authorization_denied",
            "organization_membership_change",
            "project_membership_change",
            "project_role_definition_change",
            "role_assignment_change",
            "namespace_grant_change",
            "service_credential_change",
            "audit_export"
        ]),
        selectField("outcome", "Outcome", ["all", "succeeded", "failed", "denied"]),
        numberField("limit", "Limit", "1000")
    ], "Export", form => exportAudit(form)));
    elements.detail.append(panel);
}

"use strict";
const registrationSteps = [
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
const registrationDraft = {
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
let registrationPreviewResults = [];
let registrationPreviewFingerprint = "";
function renderRegistrationList() {
    for (const step of registrationSteps) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = step.id === state.selectedRegistrationStepId ? "memory-row selected" : "memory-row";
        button.addEventListener("click", () => {
            state.selectedRegistrationStepId = step.id;
            state.selectedSource = null;
            render();
        });
        button.append(line(step.title, "memory-title"), pillRow([registrationStepStatus(step.id)]), line(registrationStepSummary(step.id), "memory-meta"));
        elements.resultList.append(button);
    }
}
function renderRegistrationDetail() {
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
function renderRegistrationSourceDetail() {
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
        elements.sourceDetail.append(heading("Registration Result"), detailGrid([
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
        ]), json);
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
    elements.sourceDetail.append(heading("Preflight Evidence"), detailGrid([
        ["Preview report", registrationDraft.accessPreviewReportId],
        ["Preview rows", registrationPreviewResults.length.toString()],
        ["Hash coverage", `${registrationSourceHashCoveragePercent()}%`],
        ["Source-link coverage", `${registrationSourceLinkCoveragePercent()}%`],
        ["Memory types", registrationCoveredMemoryTypes().join(", ")],
        ["Role-lens readiness", registrationRoleLensReadinessLabel()],
        ["Benchmark status", registrationSuccessBenchmarkStatusText()],
        ["Idempotency key", registrationDraft.idempotencyKey]
    ]), previewJson);
}
function renderRegistrationScopeStep() {
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
function renderRegistrationOwnersRolesStep() {
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
function renderRegistrationGrantsStep() {
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
function renderRegistrationSeedDocsStep() {
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
    readiness.append(heading("Seed readiness"), detailGrid(registrationSeedReadinessSummaryRows()), registrationValidationList(registrationSeedReadinessItems()), registrationSeedReadinessControls());
    section.append(docs, readiness);
    elements.detail.append(section);
}
function renderRegistrationPreflightStep() {
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
function renderRegistrationRegisterStep() {
    const section = document.createElement("section");
    section.className = "access-forms";
    section.append(heading("Register"));
    const summary = document.createElement("section");
    summary.className = "access-form";
    summary.append(detailGrid([
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
    ]), registrationTextarea("registration-note", "Note", registrationDraft.registrationNote, value => {
        registrationDraft.registrationNote = value;
        registrationDraftChanged(false);
    }), actionRow([
        registrationButton("Register", "primary-action", () => void submitRegistration()),
        registrationStepButton("Closeout", "closeout")
    ]));
    section.append(summary);
    elements.detail.append(section);
}
function renderRegistrationCloseoutStep() {
    const result = state.registrationResult;
    const section = document.createElement("section");
    section.className = "access-forms";
    section.append(heading("Closeout"));
    const status = document.createElement("section");
    status.className = "access-form";
    status.append(detailGrid([
        ["Registration", registrationSucceeded() ? readStringValue(result, "status") : "pending"],
        ["Access-boundary review", registrationSucceeded() ? "queued" : "pending"],
        ["Context retrieval check", registrationSucceeded() ? "required" : "pending"],
        ["Feedback", registrationSucceeded() ? "required" : "pending"],
        ["Seed readiness", registrationSeedReadinessStatusText()],
        ["Role-lens readiness", registrationRoleLensReadinessLabel()],
        ["Benchmark status", registrationSuccessBenchmarkStatusText()],
        ["Payload safe", registrationSucceeded() ? readBooleanValue(result, "payloadSafe") : ""],
        ["Raw source payloads", registrationSucceeded() ? readBooleanValue(result, "rawSourcePayloadsIncluded") : ""]
    ]), registrationValidationList(registrationSeedReadinessItems()), actionRow([
        linkAction("Open Access", "/admin/"),
        linkAction("Open reviews", "/reviews/")
    ]));
    const benchmark = document.createElement("section");
    benchmark.className = "access-form";
    benchmark.append(heading("Success benchmark"), detailGrid(registrationSuccessBenchmarkSummaryRows()), registrationValidationList(registrationSuccessBenchmarkItems()), registrationSuccessBenchmarkControls());
    section.append(status, benchmark);
    elements.detail.append(section);
}
function registrationOwnerRow(index, owner) {
    const row = document.createElement("div");
    row.className = "registration-row";
    row.append(registrationTextField(`registration-owner-principal-${index}`, "Principal ID", owner.principalId, value => {
        owner.principalId = value;
        registrationDraftChanged();
    }), registrationSelectField(`registration-owner-role-${index}`, "Role", owner.roleId, registrationRoleOptions(), value => {
        owner.roleId = value;
        registrationDraftChanged();
    }), registrationSelectField(`registration-owner-access-${index}`, "Access", owner.projectAccessLevel, ["reader", "contributor", "reviewer"], value => {
        owner.projectAccessLevel = value;
        registrationDraftChanged();
    }), registrationTextField(`registration-owner-label-${index}`, "Label", owner.principalLabel, value => {
        owner.principalLabel = value;
        registrationDraftChanged();
    }), registrationButton("Remove", "secondary-action", () => {
        registrationDraft.ownerAssignments.splice(index, 1);
        registrationDraftChanged();
        render();
    }));
    return row;
}
function registrationRoleRow(index, role) {
    const row = document.createElement("div");
    row.className = "registration-row";
    row.append(registrationTextField(`registration-role-id-${index}`, "Role ID", role.roleId, value => {
        role.roleId = value;
        registrationDraftChanged();
    }), registrationTextField(`registration-role-display-${index}`, "Display", role.displayName, value => {
        role.displayName = value;
        registrationDraftChanged();
    }), registrationSelectField(`registration-role-template-${index}`, "Template", role.templateRoleId, ["", ...defaultRoleIds], value => {
        role.templateRoleId = value;
        registrationDraftChanged();
    }), registrationSelectField(`registration-role-status-${index}`, "Status", role.status, ["active", "disabled"], value => {
        role.status = value;
        registrationDraftChanged();
    }), registrationTextField(`registration-role-description-${index}`, "Description", role.description, value => {
        role.description = value;
        registrationDraftChanged();
    }), registrationButton("Remove", "secondary-action", () => {
        registrationDraft.roleDefinitions.splice(index, 1);
        registrationDraftChanged();
        render();
    }));
    return row;
}
function registrationGrantRow(index, grant) {
    const row = document.createElement("div");
    row.className = "registration-row";
    row.append(registrationSelectField(`registration-grant-target-type-${index}`, "Target", grant.targetType, ["role", "principal"], value => {
        grant.targetType = value === "principal" ? "principal" : "role";
        registrationDraftChanged();
    }), registrationTextField(`registration-grant-target-id-${index}`, "Target ID", grant.targetId, value => {
        grant.targetId = value;
        registrationDraftChanged();
    }), registrationSelectField(`registration-grant-area-${index}`, "Area", grant.namespaceArea, registrationNamespaceAreas, value => {
        grant.namespaceArea = value;
        grant.namespacePrefix = registrationNamespaceFromArea(value);
        registrationDraftChanged();
        render();
    }), registrationReadOnlyField(`registration-grant-namespace-${index}`, "Namespace", grant.namespacePrefix), registrationSelectField(`registration-grant-permission-${index}`, "Permission", grant.permission, ["read", "write", "review"], value => {
        grant.permission = value;
        registrationDraftChanged();
    }), registrationButton("Remove", "secondary-action", () => {
        registrationDraft.namespaceGrants.splice(index, 1);
        registrationDraftChanged();
        render();
    }));
    return row;
}
function registrationSourceDocumentRow(index, source) {
    const row = document.createElement("div");
    row.className = "registration-row seed-doc-row";
    row.append(registrationTextField(`registration-source-path-${index}`, "Path", source.path, value => {
        source.path = value;
        registrationDraftChanged(false);
    }), registrationTextField(`registration-source-hash-${index}`, "SHA-256", source.sourceContentSha256, value => {
        source.sourceContentSha256 = value.toLowerCase();
        registrationDraftChanged(false);
    }), registrationSelectField(`registration-source-owner-${index}`, "Owner role", source.sourceOwnerRoleId, registrationRoleOptions(), value => {
        source.sourceOwnerRoleId = value;
        registrationDraftChanged(false);
    }), registrationMemoryTypeChecklist(`registration-source-memory-types-${index}`, source.memoryTypes, values => {
        source.memoryTypes = values;
        registrationDraftChanged(false);
    }), registrationButton("Remove", "secondary-action", () => {
        registrationDraft.sourceDocuments.splice(index, 1);
        registrationDraftChanged(false);
        render();
    }));
    return row;
}
function registrationSection(title, fields, actions) {
    const section = document.createElement("section");
    section.className = "access-form";
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(...fields);
    section.append(heading(title), grid, actionRow(actions));
    return section;
}
function registrationTextField(id, labelText, value, onInput) {
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
function registrationTextarea(id, labelText, value, onInput) {
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
function registrationReadOnlyField(id, labelText, value) {
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
function registrationSelectField(id, labelText, value, values, onChange) {
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
function registrationMemoryTypeChecklist(idPrefix, selectedValues, onChange) {
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
            }
            else {
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
function registrationButton(label, className, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = className;
    button.textContent = label;
    button.disabled = state.busy;
    button.addEventListener("click", onClick);
    return button;
}
function registrationStepButton(label, stepId) {
    return registrationButton(label, "secondary-action", () => {
        state.selectedRegistrationStepId = stepId;
        render();
    });
}
function registrationValidationList(items) {
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const item of items) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(item.label, "memory-title"), pillRow([item.status]), line(item.detail, "memory-meta"));
        list.append(row);
    }
    return list;
}
function registrationSeedReadinessControls() {
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(registrationSelectField("registration-seed-context-check", "Context check", registrationDraft.seedContextCheckStatus, ["pending", "passed", "failed"], value => {
        registrationDraft.seedContextCheckStatus = value === "passed" || value === "failed" ? value : "pending";
        registrationDraftChanged(false);
        render();
    }), registrationSelectField("registration-seed-feedback", "Feedback", registrationDraft.seedFeedbackStatus, ["pending", "recorded"], value => {
        registrationDraft.seedFeedbackStatus = value === "recorded" ? "recorded" : "pending";
        registrationDraftChanged(false);
        render();
    }));
    return grid;
}
function registrationSeedReadinessItems() {
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
function registrationSeedReadinessSummaryRows() {
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
function registrationSeedReadinessEvidence() {
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
function registrationSuccessBenchmarkControls() {
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(registrationReadOnlyField("registration-started-at", "Started", registrationDraft.registrationStartedAt || "pending"), registrationReadOnlyField("registration-completed-at", "Completed", registrationDraft.registrationCompletedAt || "pending"), registrationSelectField("registration-access-drift", "Access drift", registrationDraft.accessDriftStatus, ["pending", "clear", "drift_found"], value => {
        registrationDraft.accessDriftStatus = value === "clear" || value === "drift_found" ? value : "pending";
        render();
    }), registrationSelectField("registration-confidence-score", "Confidence", registrationDraft.userConfidenceScore, ["", "1", "2", "3", "4", "5"], value => {
        registrationDraft.userConfidenceScore = ["1", "2", "3", "4", "5"].includes(value) ? value : "";
        render();
    }));
    return grid;
}
function registrationSuccessBenchmarkItems() {
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
function registrationSuccessBenchmarkSummaryRows() {
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
function registrationSuccessBenchmarkEvidence() {
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
function registrationValidationItems() {
    const items = [];
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
function registrationStepStatus(stepId) {
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
function registrationStepSummary(stepId) {
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
        return registrationSucceeded() ? readStringValue(state.registrationResult, "status") : "Not submitted";
    }
    return registrationSucceeded() ? "Closeout required" : "Waiting for registration";
}
async function runRegistrationPreview() {
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
        const results = [];
        for (const request of requests) {
            const result = await apiFetch("/api/admin/access/effective-preview", {
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
    }
    catch (error) {
        registrationPreviewResults = [{ error: errorMessage(error) }];
        setStatus("Preview error");
    }
    finally {
        setBusy(false);
        render();
    }
}
async function submitRegistration() {
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
        state.registrationResult = await apiFetch("/api/admin/projects/register", {
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
    }
    catch (error) {
        state.registrationResult = { error: errorMessage(error) };
        setStatus("Registration error");
    }
    finally {
        setBusy(false);
        render();
    }
}
function registrationPreviewRequests() {
    const requests = [];
    const seen = new Set();
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
            const request = {
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
function registrationRequestBody() {
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
function compactRegistrationOwners() {
    return registrationDraft.ownerAssignments
        .map(owner => ({
        principalId: owner.principalId.trim(),
        roleId: normalizeRegistrationText(owner.roleId),
        projectAccessLevel: normalizeRegistrationText(owner.projectAccessLevel),
        principalLabel: owner.principalLabel.trim()
    }))
        .filter(owner => owner.principalId || owner.roleId || owner.principalLabel);
}
function compactRegistrationRoles() {
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
function compactRegistrationGrants() {
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
function compactRegistrationSources() {
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
function registrationSourceHashCoveragePercent() {
    const sources = compactRegistrationSources();
    if (sources.length === 0) {
        return 0;
    }
    return Math.round(100 * sources.filter(source => sha256IsValid(source.sourceContentSha256)).length / sources.length);
}
function registrationSourceLinkCoveragePercent() {
    const sources = compactRegistrationSources();
    if (sources.length === 0) {
        return 0;
    }
    return Math.round(100 * sources.filter(source => source.path
        && sha256IsValid(source.sourceContentSha256)
        && roleIsKnown(source.sourceOwnerRoleId)).length / sources.length);
}
function registrationSourceLinkCoverageRatio() {
    return registrationSourceLinkCoveragePercent() / 100;
}
function registrationSourceOwnerRoles() {
    return uniqueSorted(compactRegistrationSources().map(source => source.sourceOwnerRoleId).filter(roleIsKnown));
}
function registrationCoveredMemoryTypes() {
    return uniqueSorted(compactRegistrationSources().flatMap(source => source.memoryTypes));
}
function registrationRoleLensOwnerRoles() {
    return uniqueSorted(compactRegistrationSources()
        .filter(source => source.memoryTypes.includes("role_lens") && roleIsKnown(source.sourceOwnerRoleId))
        .map(source => source.sourceOwnerRoleId));
}
function registrationRequiredRoleLensRoles() {
    return uniqueSorted(compactRegistrationOwners()
        .map(owner => owner.roleId)
        .filter(roleId => roleIsKnown(roleId)));
}
function registrationMissingRoleLensRoles() {
    const coveredRoles = new Set(registrationRoleLensOwnerRoles());
    return registrationRequiredRoleLensRoles().filter(roleId => !coveredRoles.has(roleId));
}
function registrationRoleLensReadinessLabel() {
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
function registrationSeedReadinessStatusText() {
    const items = registrationSeedReadinessItems();
    if (items.some(item => item.status === "blocked")) {
        return "blocked";
    }
    return items.some(item => item.status === "needs_review") ? "needs_review" : "done";
}
function registrationSuccessBenchmarkStatusText() {
    const items = registrationSuccessBenchmarkItems();
    if (items.some(item => item.status === "blocked")) {
        return "blocked";
    }
    return items.some(item => item.status === "needs_review") ? "needs_review" : "done";
}
function registrationCurrentBlockingValidationCount() {
    return registrationValidationItems().filter(item => item.status === "blocked").length;
}
function registrationAccessDriftFindingCount() {
    if (registrationDraft.accessDriftStatus === "pending") {
        return null;
    }
    return registrationDraft.accessDriftStatus === "drift_found" ? 1 : 0;
}
function registrationDurationSeconds() {
    const startedAt = Date.parse(registrationDraft.registrationStartedAt);
    const completedAt = Date.parse(registrationDraft.registrationCompletedAt || new Date().toISOString());
    if (!Number.isFinite(startedAt) || !Number.isFinite(completedAt) || completedAt < startedAt) {
        return 0;
    }
    return Math.round((completedAt - startedAt) / 1000);
}
function formatRegistrationDuration() {
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
function registrationUserConfidenceScoreValue() {
    const score = Number.parseInt(registrationDraft.userConfidenceScore, 10);
    return Number.isInteger(score) && score >= 1 && score <= 5 ? score : 0;
}
function registrationUserConfidenceLabel() {
    const score = registrationUserConfidenceScoreValue();
    return score === 0 ? "pending" : `${score}/5`;
}
function registrationMemoryTypeIsCanonical(value) {
    return registrationMemoryTypes.includes(normalizeRegistrationText(value));
}
function uniqueSorted(values) {
    return [...new Set(values.filter(value => value))].sort((left, right) => left.localeCompare(right));
}
function registrationGrantIsSafe(grant, projectPrefix, customRoleIds) {
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
function syncRegistrationGrantNamespaces() {
    for (const grant of registrationDraft.namespaceGrants) {
        grant.namespacePrefix = registrationNamespaceFromArea(grant.namespaceArea);
    }
}
function registrationNamespaceFromArea(area) {
    const projectPrefix = registrationProjectPrefix();
    if (!projectPrefix) {
        return "";
    }
    return `${projectPrefix}/${area}`;
}
function registrationProjectPrefix() {
    return isGuid(registrationDraft.projectId)
        ? `/project/${registrationDraft.projectId.toLowerCase()}`
        : "";
}
function registrationRoleOptions() {
    const customRoleIds = compactRegistrationRoles().map(role => role.roleId).filter(roleId => roleId);
    return [...defaultRoleIds, ...customRoleIds];
}
function registrationMissingRequiredOwnerRoles(owners = compactRegistrationOwners()) {
    const ownerRoleIds = new Set(owners.map(owner => owner.roleId));
    return registrationRequiredOwnerRoleIds.filter(roleId => !ownerRoleIds.has(roleId));
}
function registrationOwnerAssignmentDetail(owners) {
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
function roleIsKnown(roleId) {
    return defaultRoleIds.includes(roleId) || compactRegistrationRoles().some(role => role.roleId === roleId && role.status === "active");
}
function roleIdentifierIsValid(value) {
    return /^[a-z][a-z0-9_-]{0,63}$/.test(value);
}
function sha256IsValid(value) {
    return /^[a-f0-9]{64}$/.test(value.trim().toLowerCase());
}
function isGuid(value) {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value.trim());
}
function normalizeRegistrationText(value) {
    return value.trim().toLowerCase();
}
function randomRegistrationId() {
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
function registrationDraftChanged(previewRelevant = true) {
    state.registrationResult = null;
    registrationDraft.registrationCompletedAt = "";
    registrationDraft.accessDriftStatus = "pending";
    if (previewRelevant) {
        registrationDraft.accessPreviewReportId = "";
        registrationPreviewResults = [];
        registrationPreviewFingerprint = "";
    }
}
function registrationAccessPlanFingerprint() {
    return JSON.stringify({
        projectId: registrationDraft.projectId.trim().toLowerCase(),
        roleDefinitions: compactRegistrationRoles(),
        ownerAssignments: compactRegistrationOwners(),
        namespaceGrants: compactRegistrationGrants()
    });
}
function registrationSucceeded() {
    return state.registrationResult !== null
        && typeof state.registrationResult.error !== "string"
        && readStringValue(state.registrationResult, "status") === "registered";
}
function readStringValue(value, key) {
    const item = value[key];
    return typeof item === "string" ? item : "";
}
function readBooleanValue(value, key) {
    const item = value[key];
    return typeof item === "boolean" ? (item ? "yes" : "no") : "";
}
function readNumberValue(value, key) {
    const item = value[key];
    return typeof item === "number" ? item : 0;
}

"use strict";
function createManagementViewState() {
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
async function loadManagement() {
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
    }
    catch (error) {
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
    }
    finally {
        state.management.loading = false;
        setBusy(false);
        render();
    }
}
async function refreshManagementDirectory() {
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
        apiFetch(`/api/admin/organizations?${organizationParams}`),
        apiFetch(`/api/admin/projects?${projectParams}`)
    ]);
    state.management.organizations = organizationResponse.organizations;
    state.management.projects = projectResponse.projects;
    ensureManagementSelection();
}
async function loadSelectedManagementDetail(showBusy) {
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
                apiFetch(`/api/admin/organizations/${selected.id}`),
                apiFetch(`/api/admin/organizations/${selected.id}/access-inventory`),
                apiFetch(`/api/admin/organizations/${selected.id}/management-activity?limit=20`)
            ]);
            state.management.organizationDetail = organizationDetail;
            state.management.accessInventoryDetail = accessInventory;
            state.management.managementActivityDetail = managementActivity;
            state.management.evidencePayload = managementActivity;
        }
        else {
            const projectDetail = await apiFetch(`/api/admin/projects/${selected.id}`);
            const [scopeSettings, accessInventory, roleDefinitions, grantMatrix, managementActivity] = await Promise.all([
                loadOptionalManagementDetail(apiFetch(`/api/admin/projects/${selected.id}/scope-settings`)),
                loadOptionalManagementDetail(apiFetch(`/api/admin/projects/${selected.id}/access-inventory`)),
                loadOptionalManagementDetail(apiFetch(`/api/admin/projects/${selected.id}/role-definitions`)),
                loadOptionalManagementDetail(apiFetch(`/api/admin/projects/${selected.id}/grant-matrix`)),
                loadOptionalManagementDetail(apiFetch(`/api/admin/projects/${selected.id}/management-activity?limit=20`))
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
    }
    catch (error) {
        state.management.detailError = errorMessage(error);
        setStatus("Not visible");
    }
    finally {
        if (showBusy) {
            state.management.detailLoading = false;
            setBusy(false);
            render();
        }
    }
}
async function loadOptionalManagementDetail(request) {
    try {
        return await request;
    }
    catch (error) {
        if (isExpectedOptionalManagementDetailError(error)) {
            return null;
        }
        throw error;
    }
}
function isExpectedOptionalManagementDetailError(error) {
    return error instanceof ApiFetchError
        && (error.status === 403 || error.status === 404);
}
async function refreshManagementActivityDetail() {
    const selected = state.management.selectedItem;
    if (!selected) {
        state.management.managementActivityDetail = null;
        return;
    }
    state.management.managementActivityDetail = selected.type === "organization"
        ? await apiFetch(`/api/admin/organizations/${selected.id}/management-activity?limit=20`)
        : await apiFetch(`/api/admin/projects/${selected.id}/management-activity?limit=20`);
}
async function refreshManagementActivityDetailBestEffort() {
    try {
        await refreshManagementActivityDetail();
    }
    catch {
        state.management.managementActivityDetail = null;
    }
}
function renderManagementList() {
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
        elements.resultList.append(managementSection("Organizations", state.management.organizations.map(renderManagementOrganizationRow)));
    }
    if (state.management.projects.length > 0) {
        elements.resultList.append(managementSection("Projects", state.management.projects.map(renderManagementProjectRow)));
    }
}
function renderManagementDetail() {
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
function renderManagementSourceDetail() {
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
        elements.sourceDetail.append(managementSuccessBenchmarkSection(), linkList(managementLinks()));
        return;
    }
    const benchmarkJson = document.createElement("pre");
    benchmarkJson.className = "source-json";
    benchmarkJson.textContent = JSON.stringify(managementSuccessBenchmarkEvidence(), null, 2);
    const json = document.createElement("pre");
    json.className = "source-json";
    json.textContent = JSON.stringify(detail, null, 2);
    elements.sourceDetail.append(managementSuccessBenchmarkSection(), heading("Payload-safe benchmark"), benchmarkJson, heading("Payload-safe detail"), json, linkList(managementLinks()));
}
function renderManagementOrganizationDetail(detail) {
    const organization = detail.organization;
    const counts = organization.projectStatusCounts;
    elements.detail.append(heading(organization.organizationName), detailGrid([
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
    ]), managementCountGrid([
        ["Planned", counts.planned],
        ["Active", counts.active],
        ["Archived", counts.archived],
        ["Deleted", counts.deleted]
    ]), managementAccessInventorySection(state.management.accessInventoryDetail), managementActivitySection(state.management.managementActivityDetail), actionRow(managementActions()));
}
function renderManagementProjectDetail(detail) {
    const project = detail.project;
    const evidence = detail.latestRegistrationEvidence;
    const scopeSettings = state.management.scopeSettingsDetail?.scopeSettings;
    const rows = [
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
        rows.push(["Default namespace", scopeSettings.defaultNamespacePrefix], ["Source hash required", scopeSettings.sourceHashRequired ? "yes" : "no"], ["Retention", scopeSettings.memoryRetentionClass], ["Review cadence", `${scopeSettings.reviewCadenceDays} days`], ["Settings source", scopeSettings.isDefault ? "default" : "stored"]);
    }
    if (evidence) {
        rows.push(["Registration evidence", evidence.auditEventId], ["Registered", shortDate(evidence.occurredAt)], ["Source documents", evidence.sourceDocumentCount.toString()], ["Source hash coverage", `${evidence.sourceHashCoveragePercent}%`], ["Access preview", evidence.accessPreviewReportId ?? ""], ["Audit export", evidence.auditExportId ?? ""]);
    }
    elements.detail.append(heading(project.projectName), pillRow([project.projectStatus, project.actorAccessLevel]), detailGrid(rows), managementAccessInventorySection(state.management.accessInventoryDetail), managementProjectRoleDefinitionsSection(state.management.roleDefinitionsDetail), managementGrantMatrixSection(state.management.grantMatrixDetail), managementActivitySection(state.management.managementActivityDetail), managementLifecycleForm(project), managementScopeSettingsForm(project, scopeSettings), actionRow(managementActions()));
}
function managementAccessInventorySection(inventory) {
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
    section.append(managementAccessInventoryGroup("Organization memberships", inventory.organizationMemberships.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: null,
        principalId: row.principalId,
        scopeType: "org",
        scopeId: row.organizationId,
        title: row.principalDisplayName,
        meta: `${row.accessLevel} · ${row.principalStatus} · ${row.organizationName}`,
        reviewPrompt: row.reviewPrompt
    }))), managementAccessInventoryGroup("Project memberships", inventory.projectMemberships.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: null,
        principalId: row.principalId,
        scopeType: "project",
        scopeId: row.projectId,
        title: row.principalDisplayName,
        meta: `${row.accessLevel} · ${row.principalStatus} · ${row.projectName} · ${row.projectStatus}`,
        reviewPrompt: row.reviewPrompt
    }))), managementAccessInventoryGroup("Role assignments", inventory.roleAssignments.map(row => ({
        accessRecordType: row.accessRecordType,
        accessRecordId: row.accessRecordId,
        principalId: row.principalId,
        scopeType: row.scopeType,
        scopeId: row.scopeId,
        title: `${row.principalDisplayName} · ${row.roleId}`,
        meta: `${row.scopeName} · ${row.scopeType}${row.projectStatus ? ` · ${row.projectStatus}` : ""}`,
        reviewPrompt: row.reviewPrompt
    }))), managementAccessInventoryGroup("Namespace grants", inventory.namespaceGrants.map(row => ({
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
function managementAccessInventoryGroup(title, rows) {
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
function managementAccessInventoryRecord(row) {
    const card = document.createElement("div");
    card.className = "management-access-record";
    card.append(line(row.title, "management-access-title"), line(row.meta, "management-access-meta"));
    if (row.reviewPrompt) {
        card.append(line(row.reviewPrompt, "management-access-prompt"));
    }
    const form = document.createElement("form");
    form.className = "management-revoke-form";
    form.append(managementInputField("Reason", "reason", "Revocation reason", true), managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));
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
function managementActivitySection(activity) {
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
    }
    else {
        for (const entry of activity.entries) {
            list.append(managementActivityEntry(entry));
        }
    }
    section.append(list);
    return section;
}
function managementActivityEntry(entry) {
    const card = document.createElement("section");
    card.className = "management-activity-entry";
    card.append(line(entry.summary, "management-access-title"), pillRow([entry.outcome, entry.sourceContractId ?? entry.actionType]), line(shortDate(entry.occurredAt), "management-access-meta"), line(managementActivityTargetText(entry), "management-access-meta"));
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
function managementActivityTargetText(entry) {
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
function managementProjectRoleDefinitionsSection(roleDefinitions) {
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
    }
    else {
        for (const role of roleDefinitions.roles) {
            roleList.append(managementProjectRoleDefinitionCard(role));
        }
    }
    section.append(roleList, managementNewProjectRoleDefinitionForm(roleDefinitions.project.projectId));
    return section;
}
function managementProjectRoleDefinitionCard(role) {
    const card = document.createElement("section");
    card.className = "management-role-definition";
    card.append(line(`${role.displayName} · ${role.roleId}`, "management-access-title"), pillRow([role.status, role.templateRoleId ?? "custom"]), detailGrid([
        ["Role assignments", role.assignmentCount.toString()],
        ["Role grants", role.roleGrantCount.toString()],
        ["Updated", shortDate(role.updatedAt)]
    ]));
    if (role.description) {
        card.append(line(role.description, "management-access-meta"));
    }
    if (role.status === "active" && (role.assignmentCount > 0 || role.roleGrantCount > 0)) {
        card.append(line("Disable after removing dependent role assignments and role-targeted grants.", "management-access-prompt"));
    }
    const form = document.createElement("form");
    form.className = "access-form management-role-form";
    form.append(managementReadOnlyField("Role id", role.roleId), managementInputField("Display name", "displayName", "Role display name", true, role.displayName), managementTextAreaField("Description", "description", "Role purpose and responsibility", false, role.description ?? ""), managementSelectField("Template", "templateRoleId", managementTemplateRoleOptions(), role.templateRoleId ?? ""), managementSelectField("Status", "status", managementRoleStatusOptions(), role.status), managementInputField("Reason", "reason", "Role definition change reason", true), managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));
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
function managementNewProjectRoleDefinitionForm(projectId) {
    const form = document.createElement("form");
    form.className = "access-form management-role-form management-role-create-form";
    form.append(heading("New project role"), managementInputField("Role id", "roleId", "delivery_lead", true), managementInputField("Display name", "displayName", "Delivery Lead", true), managementTextAreaField("Description", "description", "Role purpose and responsibility", false), managementSelectField("Template", "templateRoleId", managementTemplateRoleOptions(), ""), managementSelectField("Status", "status", managementRoleStatusOptions(), "active"), managementInputField("Reason", "reason", "Role definition reason", true), managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));
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
function managementTemplateRoleOptions() {
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
function managementRoleStatusOptions() {
    return [
        ["active", "Active"],
        ["disabled", "Disabled"]
    ];
}
function managementGrantMatrixSection(matrix) {
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
function managementGrantMatrixRoleCard(matrix, role) {
    const card = document.createElement("section");
    card.className = "management-grant-role";
    card.append(line(`${role.displayName} · ${role.roleId}`, "management-access-title"), line(`${role.status} · ${role.recommendedPresetId} · ${grantMatrixAlignmentLabel(role.presetAlignment)}`, "management-access-meta"));
    if (role.description) {
        card.append(line(role.description, "management-access-meta"));
    }
    const grants = document.createElement("div");
    grants.className = "management-grant-list";
    if (role.grants.length === 0) {
        grants.append(line("No role-targeted grants", "memory-meta"));
    }
    else {
        for (const grant of role.grants) {
            grants.append(line(`${grant.permission} · ${grant.namespacePrefix}`, "management-grant-row"));
        }
    }
    const previews = document.createElement("div");
    previews.className = "management-grant-previews";
    const allowedPreviewCount = role.effectiveAccessPreviews.filter(preview => preview.allowed).length;
    previews.append(line(`${allowedPreviewCount}/${role.effectiveAccessPreviews.length} effective previews allowed`, "management-access-meta"));
    for (const preview of role.effectiveAccessPreviews.slice(0, 4)) {
        previews.append(line(`${preview.allowed ? "allowed" : "denied"} · ${preview.permission} · ${preview.namespacePrefix}`, preview.allowed ? "management-grant-preview-good" : "management-grant-preview-warn"));
    }
    const form = document.createElement("form");
    form.className = "access-form management-grant-form";
    const presetField = managementSelectField("Preset", "presetId", [
        ["custom", "Custom"],
        ...matrix.presets.map(preset => [preset.presetId, preset.displayName])
    ], role.presetAlignment === "matches_preset" ? role.recommendedPresetId : "custom");
    const grantRowsField = managementTextAreaField("Grant rows", "grantRows", "read /project/.../facts", true, grantMatrixRowsText(role.grants));
    const grantRows = grantRowsField.querySelector("textarea");
    const presetSelect = presetField.querySelector("select");
    presetSelect.addEventListener("change", () => {
        const preset = matrix.presets.find(candidate => candidate.presetId === presetSelect.value);
        if (preset) {
            grantRows.value = grantMatrixPresetRowsText(matrix.project.projectId, role.roleId, preset);
        }
    });
    form.append(presetField, grantRowsField, managementInputField("Reason", "reason", "Grant matrix reason", true), managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));
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
function managementSuccessBenchmarkSection() {
    const section = document.createElement("section");
    section.className = "management-success-benchmark";
    section.append(heading("Management success benchmark"), detailGrid(managementSuccessBenchmarkSummaryRows()), managementSuccessBenchmarkItemsList(), managementSuccessBenchmarkControls());
    return section;
}
function managementSuccessBenchmarkItemsList() {
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const item of managementSuccessBenchmarkItems()) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(item.label, "memory-title"), pillRow([item.status]), line(item.detail, "memory-meta"));
        list.append(row);
    }
    return list;
}
function managementSuccessBenchmarkControls() {
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(managementReadOnlyField("Benchmark started", state.management.benchmarkStartedAt || "pending"), managementReadOnlyField("First detail loaded", state.management.benchmarkFirstDetailLoadedAt || "pending"), managementBenchmarkSelectField("Access drift", state.management.benchmarkAccessDriftStatus, ["pending", "clear", "drift_found"], value => {
        state.management.benchmarkAccessDriftStatus = value === "clear" || value === "drift_found" ? value : "pending";
        render();
    }), managementBenchmarkSelectField("SQL fallback", state.management.benchmarkSqlFallbackStatus, ["none", "used"], value => {
        state.management.benchmarkSqlFallbackStatus = value === "used" ? "used" : "none";
        render();
    }), managementBenchmarkSelectField("Confidence", state.management.benchmarkUserConfidenceScore, ["", "1", "2", "3", "4", "5"], value => {
        state.management.benchmarkUserConfidenceScore = ["1", "2", "3", "4", "5"].includes(value) ? value : "";
        render();
    }));
    return grid;
}
function managementSuccessBenchmarkItems() {
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
function managementSuccessBenchmarkSummaryRows() {
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
function managementSuccessBenchmarkEvidence() {
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
function managementLifecycleForm(project) {
    const form = document.createElement("form");
    form.className = "access-form management-form";
    form.append(heading("Lifecycle"));
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(managementSelectField("Project status", "projectStatus", [
        ["planned", "Planned"],
        ["active", "Active"],
        ["archived", "Archived"],
        ["deleted", "Deleted"]
    ], project.projectStatus), managementInputField("Reason", "reason", "Lifecycle change reason", true), managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));
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
function managementScopeSettingsForm(project, scopeSettings) {
    const form = document.createElement("form");
    form.className = "access-form management-form";
    form.append(heading("Scope settings"));
    const defaultNamespacePrefix = scopeSettings?.defaultNamespacePrefix ?? `/project/${project.projectId}/facts`;
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(managementInputField("Default namespace", "defaultNamespacePrefix", `/project/${project.projectId}/facts`, true, defaultNamespacePrefix), managementCheckboxField("Source hash required", "sourceHashRequired", scopeSettings?.sourceHashRequired ?? true), managementSelectField("Retention", "memoryRetentionClass", [
        ["ephemeral", "Ephemeral"],
        ["standard", "Standard"],
        ["audit", "Audit"],
        ["legal_hold", "Legal hold"]
    ], scopeSettings?.memoryRetentionClass ?? "standard"), managementNumberField("Review cadence", "reviewCadenceDays", scopeSettings?.reviewCadenceDays ?? 7), managementInputField("Reason", "reason", "Scope settings reason", true), managementInputField("Audit evidence", "auditEvidenceId", "change-ticket-123", true));
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
function managementInputField(labelText, name, placeholder, required, value = "") {
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
function managementTextAreaField(labelText, name, placeholder, required, value = "") {
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
function managementNumberField(labelText, name, value) {
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
function managementSelectField(labelText, name, options, selectedValue) {
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
function managementCheckboxField(labelText, name, checked) {
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
function managementReadOnlyField(labelText, value) {
    const label = document.createElement("label");
    const span = document.createElement("span");
    span.textContent = labelText;
    const input = document.createElement("input");
    input.readOnly = true;
    input.value = value;
    label.append(span, input);
    return label;
}
function managementBenchmarkSelectField(labelText, value, values, onChange) {
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
function managementSection(title, rows) {
    const section = document.createElement("section");
    section.className = "management-section";
    section.append(line(title, "management-section-title"), ...rows);
    return section;
}
function renderManagementOrganizationRow(organization) {
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
    button.append(line(organization.organizationName, "memory-title"), pillRow([organization.actorAccessLevel]), line(`${organization.projectCount} projects · ${organization.organizationMembershipCount} org members`, "memory-meta"), line(managementProjectStatusText(organization.projectStatusCounts), "memory-date"));
    return button;
}
function renderManagementProjectRow(project) {
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
    button.append(line(project.projectName, "memory-title"), pillRow([project.projectStatus, project.actorAccessLevel]), line(project.organizationName, "memory-meta"), line(`${project.projectMembershipCount} members · ${project.namespaceGrantCount} grants`, "memory-date"));
    return button;
}
function managementCountGrid(items) {
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
function managementActions() {
    return [
        managementModeButton("Project Registration", "registration"),
        managementModeButton("Access Management", "access"),
        managementRefreshButton()
    ];
}
function managementModeButton(label, mode) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "secondary-action";
    button.textContent = label;
    button.disabled = state.busy;
    button.addEventListener("click", () => switchManagementMode(mode));
    return button;
}
function managementRefreshButton() {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "primary-action";
    button.textContent = "Refresh";
    button.disabled = state.busy;
    button.addEventListener("click", () => void loadManagement());
    return button;
}
function switchManagementMode(mode) {
    state.mode = mode;
    elements.modeFilter.value = mode;
    state.selectedSource = null;
    updateFilterVisibility();
    render();
    void loadCurrentMode();
}
function managementLinks() {
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
function ensureManagementSelection() {
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
function isVisibleManagementSelection(selection) {
    if (selection.type === "organization") {
        return state.management.organizations.some(organization => organization.organizationId === selection.id);
    }
    return state.management.projects.some(project => project.projectId === selection.id);
}
function isSelectedManagementItem(type, id) {
    const selected = state.management.selectedItem;
    return selected?.type === type && selected.id === id;
}
function managementProjectStatusText(counts) {
    return `${counts.active} active · ${counts.planned} planned · ${counts.archived} archived · ${counts.deleted} deleted`;
}
function ensureManagementBenchmarkStarted() {
    if (!state.management.benchmarkStartedAt) {
        state.management.benchmarkStartedAt = new Date().toISOString();
    }
}
function markManagementDetailLoaded() {
    ensureManagementBenchmarkStarted();
    if (!state.management.benchmarkFirstDetailLoadedAt) {
        state.management.benchmarkFirstDetailLoadedAt = new Date().toISOString();
    }
}
function markManagementModificationCompleted(operation) {
    ensureManagementBenchmarkStarted();
    state.management.benchmarkLastModificationCompletedAt = new Date().toISOString();
    state.management.benchmarkLatestOperation = operation;
}
function markManagementOperationFailed() {
    ensureManagementBenchmarkStarted();
    state.management.benchmarkOperationFailureCount += 1;
}
function managementSuccessBenchmarkStatusText() {
    const items = managementSuccessBenchmarkItems();
    if (items.some(item => item.status === "blocked")) {
        return "blocked";
    }
    return items.some(item => item.status === "needs_review") ? "needs_review" : "done";
}
function managementSelectedScopeEvidence() {
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
function managementFindInspectDurationSeconds() {
    return managementSecondsBetween(state.management.benchmarkStartedAt, state.management.benchmarkFirstDetailLoadedAt);
}
function managementModificationDurationSeconds() {
    return managementSecondsBetween(state.management.benchmarkStartedAt, state.management.benchmarkLastModificationCompletedAt);
}
function managementSecondsBetween(startValue, endValue) {
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
function formatManagementDuration(seconds) {
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
function managementAccessDriftFindingCount() {
    if (state.management.benchmarkAccessDriftStatus === "pending") {
        return null;
    }
    if (state.management.benchmarkAccessDriftStatus === "clear") {
        return 0;
    }
    return Math.max(1, state.management.accessInventoryDetail?.counts.staleAccessPrompts ?? 0);
}
function managementRawPayloadLeakageCount() {
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
function managementUserConfidenceScoreValue() {
    const score = Number.parseInt(state.management.benchmarkUserConfidenceScore, 10);
    return Number.isInteger(score) && score >= 1 && score <= 5 ? score : 0;
}
function managementUserConfidenceLabel() {
    const score = managementUserConfidenceScoreValue();
    return score === 0 ? "pending" : `${score}/5`;
}
function grantMatrixAlignmentLabel(alignment) {
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
function grantMatrixRowsText(grants) {
    return grants
        .map(grant => `${grant.permission} ${grant.namespacePrefix}`)
        .join("\n");
}
function grantMatrixPresetRowsText(projectId, roleId, preset) {
    return preset.grants
        .map(grant => `${grant.permission} ${resolveGrantMatrixTemplate(projectId, roleId, grant.namespaceTemplate)}`)
        .join("\n");
}
function resolveGrantMatrixTemplate(projectId, roleId, namespaceTemplate) {
    return namespaceTemplate
        .replaceAll("{projectRoot}", `/project/${projectId}`)
        .replaceAll("{roleId}", roleId);
}
function parseGrantMatrixRows(value) {
    const rows = [];
    const seen = new Set();
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
async function updateManagementLifecycle(projectId, form) {
    setBusy(true);
    setStatus("Saving lifecycle");
    try {
        const response = await apiFetch(`/api/admin/projects/${projectId}/lifecycle`, {
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
    }
    catch (error) {
        markManagementOperationFailed();
        state.management.evidencePayload = { error: errorMessage(error) };
        setStatus("Lifecycle error");
    }
    finally {
        setBusy(false);
        render();
    }
}
async function updateManagementGrantMatrixRole(projectId, role, form) {
    setBusy(true);
    setStatus("Saving grant matrix");
    try {
        const response = await apiFetch(`/api/admin/projects/${projectId}/grant-matrix/roles/${encodeURIComponent(role.roleId)}`, {
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
    }
    catch (error) {
        markManagementOperationFailed();
        state.management.evidencePayload = { error: errorMessage(error) };
        setStatus("Grant matrix error");
    }
    finally {
        setBusy(false);
        render();
    }
}
async function updateManagementProjectRoleDefinition(projectId, roleId, form) {
    setBusy(true);
    setStatus("Saving role definition");
    try {
        const response = await apiFetch(`/api/admin/projects/${projectId}/role-definitions/${encodeURIComponent(roleId)}`, {
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
    }
    catch (error) {
        markManagementOperationFailed();
        state.management.evidencePayload = { error: errorMessage(error) };
        setStatus("Role definition error");
    }
    finally {
        setBusy(false);
        render();
    }
}
async function updateManagementScopeSettings(projectId, form) {
    setBusy(true);
    setStatus("Saving settings");
    try {
        const response = await apiFetch(`/api/admin/projects/${projectId}/scope-settings`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                defaultNamespacePrefix: formValue(form, "defaultNamespacePrefix"),
                sourceHashRequired: form.elements.namedItem("sourceHashRequired")?.checked ?? false,
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
    }
    catch (error) {
        markManagementOperationFailed();
        state.management.evidencePayload = { error: errorMessage(error) };
        setStatus("Settings error");
    }
    finally {
        setBusy(false);
        render();
    }
}
async function revokeManagementAccess(target, form) {
    setBusy(true);
    setStatus("Revoking access");
    try {
        const response = await apiFetch("/api/admin/access/revocations", {
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
    }
    catch (error) {
        markManagementOperationFailed();
        state.management.evidencePayload = { error: errorMessage(error) };
        setStatus("Revocation error");
    }
    finally {
        setBusy(false);
        render();
    }
}
function applyManagementLifecycleResponse(response) {
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

"use strict";
class ApiFetchError extends Error {
    status;
    constructor(status, detail) {
        super(`${status} ${detail}`);
        this.status = status;
        this.name = "ApiFetchError";
    }
}
const credentialStorageKey = "memorySystem.consoleCredential";
const credentialKindStorageKey = "memorySystem.consoleCredentialKind";
const consoleReturnUrl = "/admin/";
const state = {
    mode: "memory",
    facts: [],
    events: [],
    operationsSummary: null,
    selectedOperationId: null,
    complianceStatus: null,
    pilotReadiness: null,
    selectedFactId: null,
    selectedEventId: null,
    selectedComplianceId: null,
    selectedPilotGateId: null,
    selectedRegistrationStepId: "scope",
    selectedSource: null,
    accessResult: null,
    registrationResult: null,
    management: createManagementViewState(),
    busy: false
};
const elements = {
    apiBase: byId("api-base"),
    apiKey: byId("api-key"),
    credentialState: byId("credential-state"),
    modeFilter: byId("mode-filter"),
    statusFilter: byId("status-filter"),
    projectStatusFilter: byId("project-status-filter"),
    memoryTypeFilter: byId("memory-type-filter"),
    roleFilter: byId("role-filter"),
    namespacePrefixFilter: byId("namespace-prefix-filter"),
    eventTypeFilter: byId("event-type-filter"),
    retentionFilter: byId("retention-filter"),
    sensitivityFilter: byId("sensitivity-filter"),
    trustFilter: byId("trust-filter"),
    redactionFilter: byId("redaction-filter"),
    scopeTypeFilter: byId("scope-type-filter"),
    scopeIdFilter: byId("scope-id-filter"),
    createdFromFilter: byId("created-from-filter"),
    createdToFilter: byId("created-to-filter"),
    query: byId("query"),
    refresh: byId("refresh"),
    logout: byId("logout"),
    status: byId("status"),
    listTitle: byId("list-title"),
    resultList: byId("result-list"),
    detail: byId("detail"),
    sourceTitle: byId("source-title"),
    sourceDetail: byId("source-detail")
};
elements.apiBase.value = window.location.origin;
elements.apiKey.value = sessionStorage.getItem(credentialStorageKey) ?? "";
updateCredentialState();
redirectToLoginIfMissingCredential(consoleReturnUrl);
elements.refresh.addEventListener("click", () => void loadCurrentMode());
elements.logout.addEventListener("click", event => {
    event.preventDefault();
    void logout();
});
elements.apiKey.addEventListener("change", () => {
    persistCredential();
    updateCredentialState();
});
elements.query.addEventListener("keydown", event => {
    if (event.key === "Enter") {
        event.preventDefault();
        void loadCurrentMode();
    }
});
elements.statusFilter.addEventListener("change", () => void loadFacts());
elements.projectStatusFilter.addEventListener("change", () => void loadManagement());
elements.modeFilter.addEventListener("change", () => {
    state.mode = readMode();
    state.selectedSource = null;
    updateFilterVisibility();
    render();
    void loadCurrentMode();
});
for (const element of [
    elements.memoryTypeFilter,
    elements.roleFilter,
    elements.namespacePrefixFilter,
    elements.eventTypeFilter,
    elements.retentionFilter,
    elements.sensitivityFilter,
    elements.trustFilter,
    elements.redactionFilter,
    elements.scopeTypeFilter,
    elements.createdFromFilter,
    elements.createdToFilter
]) {
    element.addEventListener("change", () => void loadCurrentMode());
}
elements.scopeIdFilter.addEventListener("keydown", event => {
    if (event.key === "Enter") {
        event.preventDefault();
        void loadCurrentMode();
    }
});
elements.namespacePrefixFilter.addEventListener("keydown", event => {
    if (event.key === "Enter") {
        event.preventDefault();
        void loadCurrentMode();
    }
});
updateFilterVisibility();
render();
function byId(id) {
    const element = document.getElementById(id);
    if (!element) {
        throw new Error(`Missing element '${id}'.`);
    }
    return element;
}
function readMode() {
    if (elements.modeFilter.value === "events") {
        return "events";
    }
    if (elements.modeFilter.value === "access") {
        return "access";
    }
    if (elements.modeFilter.value === "registration") {
        return "registration";
    }
    if (elements.modeFilter.value === "operations") {
        return "operations";
    }
    if (elements.modeFilter.value === "management") {
        return "management";
    }
    if (elements.modeFilter.value === "compliance") {
        return "compliance";
    }
    if (elements.modeFilter.value === "pilot") {
        return "pilot";
    }
    return "memory";
}
async function loadCurrentMode() {
    if (state.mode === "events") {
        await loadEvents();
        return;
    }
    if (state.mode === "compliance") {
        await loadCompliance();
        return;
    }
    if (state.mode === "operations") {
        await loadOperations();
        return;
    }
    if (state.mode === "pilot") {
        await loadPilotReadiness();
        return;
    }
    if (state.mode === "management") {
        await loadManagement();
        return;
    }
    if (state.mode === "access") {
        setStatus("Access");
        render();
        return;
    }
    if (state.mode === "registration") {
        setStatus("Registration");
        render();
        return;
    }
    await loadFacts();
}
async function loadFacts() {
    setBusy(true);
    setStatus("Loading");
    state.selectedSource = null;
    try {
        const params = commonParams();
        params.set("limit", "50");
        if (elements.statusFilter.value !== "all") {
            params.set("status", elements.statusFilter.value);
        }
        addSelectParam(params, "memoryType", elements.memoryTypeFilter);
        const namespacePrefix = elements.namespacePrefixFilter.value.trim();
        if (namespacePrefix) {
            params.set("namespacePrefix", namespacePrefix);
        }
        const response = await apiFetch(`/api/admin/memory/facts?${params}`);
        state.facts = response.facts;
        state.selectedFactId = response.facts[0]?.id ?? null;
        setStatus(`${response.facts.length} facts`);
    }
    catch (error) {
        setStatus("Error");
        state.facts = [];
        state.selectedFactId = null;
        state.selectedSource = null;
        elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
    }
    finally {
        setBusy(false);
        render();
    }
}
async function loadOperations() {
    setBusy(true);
    setStatus("Loading");
    state.selectedSource = null;
    try {
        const response = await apiFetch("/api/operations/summary");
        state.operationsSummary = response;
        state.selectedOperationId = state.selectedOperationId ?? "readiness";
        setStatus(`Ops ${response.status}`);
    }
    catch (error) {
        setStatus("Error");
        state.operationsSummary = null;
        state.selectedOperationId = null;
        state.selectedSource = null;
        elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
    }
    finally {
        setBusy(false);
        render();
    }
}
async function loadEvents() {
    setBusy(true);
    setStatus("Loading");
    state.selectedSource = null;
    try {
        const params = commonParams();
        params.set("limit", "50");
        addSelectParam(params, "eventType", elements.eventTypeFilter);
        addSelectParam(params, "retentionClass", elements.retentionFilter);
        addSelectParam(params, "sensitivity", elements.sensitivityFilter);
        addSelectParam(params, "trustLevel", elements.trustFilter);
        addSelectParam(params, "redactionStatus", elements.redactionFilter);
        addDateParam(params, "createdFrom", elements.createdFromFilter.value);
        addDateParam(params, "createdTo", elements.createdToFilter.value);
        const response = await apiFetch(`/api/admin/source-events?${params}`);
        state.events = response.events;
        state.selectedEventId = response.events[0]?.id ?? null;
        setStatus(`${response.events.length} sources`);
    }
    catch (error) {
        setStatus("Error");
        state.events = [];
        state.selectedEventId = null;
        state.selectedSource = null;
        elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
    }
    finally {
        setBusy(false);
        render();
    }
}
async function loadCompliance() {
    setBusy(true);
    setStatus("Loading");
    state.selectedSource = null;
    try {
        const response = await apiFetch("/api/admin/compliance/status");
        state.complianceStatus = response;
        state.selectedComplianceId = response.items[0]?.id ?? null;
        setStatus(`${response.items.length} checks`);
    }
    catch (error) {
        setStatus("Error");
        state.complianceStatus = null;
        state.selectedComplianceId = null;
        state.selectedSource = null;
        elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
    }
    finally {
        setBusy(false);
        render();
    }
}
async function loadPilotReadiness() {
    setBusy(true);
    setStatus("Loading");
    state.selectedSource = null;
    try {
        const response = await apiFetch("/api/admin/pilot/readiness");
        state.pilotReadiness = response;
        state.selectedPilotGateId = response.gates[0]?.id ?? null;
        setStatus(response.decision.externalInviteApproved ? "Pilot GO" : "Pilot NO-GO");
    }
    catch (error) {
        setStatus("Error");
        state.pilotReadiness = null;
        state.selectedPilotGateId = null;
        state.selectedSource = null;
        elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
    }
    finally {
        setBusy(false);
        render();
    }
}
function commonParams() {
    const params = new URLSearchParams();
    const scopeType = elements.scopeTypeFilter.value.trim();
    const scopeId = elements.scopeIdFilter.value.trim();
    if (scopeType && scopeId) {
        params.set("scopeType", scopeType);
        params.set("scopeId", scopeId);
    }
    const query = elements.query.value.trim();
    if (query) {
        params.set("q", query);
    }
    addSelectParam(params, "roleId", elements.roleFilter);
    return params;
}
function addSelectParam(params, key, element) {
    if (element.value !== "all") {
        params.set(key, element.value);
    }
}
function addDateParam(params, key, value) {
    if (!value) {
        return;
    }
    params.set(key, new Date(value).toISOString());
}
async function openSource(path) {
    setBusy(true);
    setStatus("Opening source");
    try {
        state.selectedSource = await apiFetch(path);
        setStatus("Source opened");
    }
    catch (error) {
        state.selectedSource = null;
        elements.sourceDetail.replaceChildren(emptyPanel(errorMessage(error)));
    }
    finally {
        setBusy(false);
        renderSourceDetail();
    }
}
async function apiFetch(path, init = {}) {
    const apiBase = elements.apiBase.value.trim().replace(/\/$/, "");
    const headers = new Headers(init.headers);
    applyAuth(headers);
    const response = await fetch(`${apiBase}${path}`, {
        ...init,
        credentials: "same-origin",
        headers
    });
    const text = await response.text();
    let payload = null;
    try {
        payload = text ? JSON.parse(text) : null;
    }
    catch (error) {
        if (response.ok) {
            throw error;
        }
    }
    if (!response.ok) {
        const detail = responseDetail(payload, response.statusText, text);
        if (isAuthenticationFailure(response)) {
            redirectToLoginAfterAuthenticationFailure();
        }
        throw new ApiFetchError(response.status, detail);
    }
    return payload;
}
function responseDetail(payload, fallback, text) {
    if (payload && typeof payload === "object") {
        const record = payload;
        const detail = record.detail ?? record.title;
        if (typeof detail === "string" && detail.trim()) {
            return detail;
        }
    }
    return text.trim() || fallback;
}
async function logout() {
    setBusy(true);
    clearStoredCredential();
    elements.apiKey.value = "";
    window.location.assign(logoutUrl(consoleReturnUrl));
    setBusy(false);
}
function redirectToLoginIfMissingCredential(returnUrl) {
    if (readCredential()) {
        return;
    }
    window.location.replace(loginUrl(returnUrl));
}
function redirectToLoginAfterAuthenticationFailure() {
    clearStoredCredential();
    elements.apiKey.value = "";
    updateCredentialState();
    window.location.replace(loginUrl(consoleReturnUrl));
}
function clearStoredCredential() {
    sessionStorage.removeItem(credentialStorageKey);
    sessionStorage.removeItem(credentialKindStorageKey);
}
function isAuthenticationFailure(response) {
    return response.status === 401;
}
function loginUrl(returnUrl) {
    return `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}
function logoutUrl(returnUrl) {
    return `/auth/logout?returnUrl=${encodeURIComponent(returnUrl)}`;
}
function persistCredential() {
    const credential = elements.apiKey.value.trim();
    if (credential) {
        sessionStorage.setItem(credentialStorageKey, credential);
        sessionStorage.setItem(credentialKindStorageKey, looksLikeJwt(credential) ? "jwt" : "api_key");
    }
    else {
        clearStoredCredential();
    }
}
function applyAuth(headers) {
    const credential = readCredential();
    if (!credential) {
        return;
    }
    if (!readStoredCredential()) {
        persistCredential();
    }
    if (usesBearerCredential(credential)) {
        headers.set("Authorization", `Bearer ${credential}`);
        headers.delete("X-Api-Key");
        return;
    }
    headers.set("X-Api-Key", credential);
    headers.delete("Authorization");
}
function readCredential() {
    const storedCredential = readStoredCredential();
    if (storedCredential) {
        if (elements.apiKey.value.trim() !== storedCredential) {
            elements.apiKey.value = storedCredential;
            updateCredentialState();
        }
        return storedCredential;
    }
    return elements.apiKey.value.trim();
}
function readStoredCredential() {
    return sessionStorage.getItem(credentialStorageKey)?.trim() ?? "";
}
function usesBearerCredential(credential) {
    const credentialKind = sessionStorage.getItem(credentialKindStorageKey);
    return credentialKind === "oidc_jwt"
        || credentialKind === "jwt"
        || (!credentialKind && looksLikeJwt(credential));
}
function looksLikeJwt(value) {
    return value.split(".").length === 3;
}
function updateCredentialState() {
    const credential = elements.apiKey.value.trim();
    if (!credential) {
        elements.credentialState.textContent = "Missing";
        return;
    }
    elements.credentialState.textContent = looksLikeJwt(credential) ? "JWT" : "API key";
}
function render() {
    elements.listTitle.textContent = state.mode === "events"
        ? "Source Events"
        : state.mode === "pilot"
            ? "Pilot Gates"
            : state.mode === "operations"
                ? "Operations"
                : state.mode === "management"
                    ? "Organizations & Projects"
                    : state.mode === "compliance"
                        ? "Compliance"
                        : state.mode === "registration"
                            ? "Registration Steps"
                            : state.mode === "access"
                                ? "Access Actions"
                                : "Memory Facts";
    elements.sourceTitle.textContent = state.mode === "events"
        ? "Payload"
        : state.mode === "pilot"
            ? "Pilot Work"
            : state.mode === "operations"
                ? "Operator Links"
                : state.mode === "management"
                    ? "Management Evidence"
                    : state.mode === "compliance"
                        ? "Evidence Links"
                        : state.mode === "registration"
                            ? "Registration Evidence"
                            : state.mode === "access"
                                ? "Result"
                                : "Source Evidence";
    renderResultList();
    renderDetail();
    renderSourceDetail();
}
function renderResultList() {
    elements.resultList.replaceChildren();
    if (state.mode === "events") {
        renderEventList();
        return;
    }
    if (state.mode === "compliance") {
        renderComplianceList();
        return;
    }
    if (state.mode === "operations") {
        renderOperationsList();
        return;
    }
    if (state.mode === "pilot") {
        renderPilotList();
        return;
    }
    if (state.mode === "management") {
        renderManagementList();
        return;
    }
    if (state.mode === "access") {
        renderAccessList();
        return;
    }
    if (state.mode === "registration") {
        renderRegistrationList();
        return;
    }
    renderMemoryList();
}
function renderAccessList() {
    for (const action of [
        "Organization membership",
        "Project membership",
        "Project role definition",
        "Role assignment",
        "Namespace grant",
        "Break-glass admin",
        "Effective preview",
        "Audit export"
    ]) {
        const row = document.createElement("div");
        row.className = "memory-row";
        row.append(line(action, "memory-title"));
        elements.resultList.append(row);
    }
}
function renderPilotList() {
    const status = state.pilotReadiness;
    const gates = status?.gates ?? [];
    if (!status || gates.length === 0) {
        elements.resultList.append(emptyPanel("No pilot readiness status"));
        return;
    }
    for (const gate of gates) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = gate.id === state.selectedPilotGateId ? "memory-row selected" : "memory-row";
        button.addEventListener("click", () => {
            state.selectedPilotGateId = gate.id;
            state.selectedSource = null;
            render();
        });
        button.append(line(`${gate.id} ${gate.title}`, "memory-title"), pillRow([gate.priority, gate.status, gate.blocksExternalInvite ? "blocks_invite" : "not_blocking"]), line(gate.summary, "memory-meta"), line(`${gate.evidence.length} evidence link${gate.evidence.length === 1 ? "" : "s"}`, "memory-date"));
        elements.resultList.append(button);
    }
}
function renderComplianceList() {
    const items = state.complianceStatus?.items ?? [];
    if (items.length === 0) {
        elements.resultList.append(emptyPanel("No compliance status"));
        return;
    }
    for (const item of items) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = item.id === state.selectedComplianceId ? "memory-row selected" : "memory-row";
        button.addEventListener("click", () => {
            state.selectedComplianceId = item.id;
            state.selectedSource = null;
            render();
        });
        button.append(line(item.title, "memory-title"), pillRow([item.status, item.evidenceKind]), line(item.summary, "memory-meta"), line(item.count === null ? "Linked evidence" : `${item.count} visible`, "memory-date"));
        elements.resultList.append(button);
    }
}
function renderOperationsList() {
    const summary = state.operationsSummary;
    if (!summary) {
        elements.resultList.append(emptyPanel("No operations summary"));
        return;
    }
    for (const item of operationListItems(summary)) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = item.id === state.selectedOperationId ? "memory-row selected" : "memory-row";
        button.addEventListener("click", () => {
            state.selectedOperationId = item.id;
            state.selectedSource = null;
            render();
        });
        button.append(line(item.title, "memory-title"), pillRow(item.pills), line(item.summary, "memory-meta"), line(item.detail, "memory-date"));
        elements.resultList.append(button);
    }
}
function renderMemoryList() {
    if (state.facts.length === 0) {
        elements.resultList.append(emptyPanel("No memory facts"));
        return;
    }
    for (const fact of state.facts) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = fact.id === state.selectedFactId ? "memory-row selected" : "memory-row";
        button.addEventListener("click", () => {
            state.selectedFactId = fact.id;
            state.selectedSource = null;
            render();
        });
        button.append(line(displaySubject(fact), "memory-title"), pillRow([fact.status, fact.memoryType]), line(`${fact.scopeType}:${fact.scopeId}`, "memory-meta"), line(shortDate(fact.updatedAt), "memory-date"));
        elements.resultList.append(button);
    }
}
function renderEventList() {
    if (state.events.length === 0) {
        elements.resultList.append(emptyPanel("No source events"));
        return;
    }
    for (const sourceEvent of state.events) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = sourceEvent.id === state.selectedEventId ? "memory-row selected" : "memory-row";
        button.addEventListener("click", () => {
            state.selectedEventId = sourceEvent.id;
            state.selectedSource = null;
            render();
        });
        button.append(line(sourceEvent.eventType, "memory-title"), pillRow([sourceEvent.retentionClass, sourceEvent.sensitivity, sourceEvent.redactionStatus]), line(`${sourceEvent.scope.scopeType}:${sourceEvent.scope.scopeId}`, "memory-meta"), line(`${sourceEvent.references.length} refs · ${shortDate(sourceEvent.createdAt)}`, "memory-date"));
        elements.resultList.append(button);
    }
}
function renderDetail() {
    elements.detail.replaceChildren();
    if (state.mode === "events") {
        renderEventDetail();
        return;
    }
    if (state.mode === "compliance") {
        renderComplianceDetail();
        return;
    }
    if (state.mode === "operations") {
        renderOperationsDetail();
        return;
    }
    if (state.mode === "pilot") {
        renderPilotDetail();
        return;
    }
    if (state.mode === "management") {
        renderManagementDetail();
        return;
    }
    if (state.mode === "access") {
        renderAccessDetail();
        return;
    }
    if (state.mode === "registration") {
        renderRegistrationDetail();
        return;
    }
    renderMemoryDetail();
}
function renderPilotDetail() {
    const status = state.pilotReadiness;
    const gate = selectedPilotGate();
    if (!status || !gate) {
        elements.detail.append(emptyPanel("Select a pilot gate"));
        return;
    }
    const rows = [
        ["Decision", status.decision.status],
        ["External invite", status.decision.externalInviteApproved ? "approved" : "blocked"],
        ["Gate", gate.id],
        ["Priority", gate.priority],
        ["Status", gate.status],
        ["Blocks invite", gate.blocksExternalInvite ? "yes" : "no"],
        ["Payload safe", status.payloadSafe ? "yes" : "no"],
        ["Raw source payloads", status.rawSourcePayloadsIncluded ? "included" : "not included"],
        ["Status id", status.statusId],
        ["Generated", status.generatedAt]
    ];
    elements.detail.append(heading(`${gate.id} ${gate.title}`), paragraph(status.decision.reason, status.decision.externalInviteApproved ? "memory-object" : "memory-object hidden-content"), paragraph(gate.summary, "memory-object"), detailGrid(rows), evidenceList(gate.evidence, "Evidence"), missingInputList(gate.missingInputs));
}
function renderComplianceDetail() {
    const status = state.complianceStatus;
    const item = selectedComplianceItem();
    if (!status || !item) {
        elements.detail.append(emptyPanel("Select a compliance check"));
        return;
    }
    const rows = [
        ["Check", item.id],
        ["Status", item.status],
        ["Evidence kind", item.evidenceKind],
        ["Count", item.count === null ? "" : item.count.toString()],
        ["Payload safe", item.payloadSafe ? "yes" : "no"],
        ["Raw source payloads", item.rawSourcePayloadsIncluded ? "included" : "not included"],
        ["Generated", shortDate(status.generatedAt)]
    ];
    elements.detail.append(heading(item.title), paragraph(item.summary, "memory-object"), detailGrid(rows), metricList(item.metrics));
}
function renderOperationsDetail() {
    const summary = state.operationsSummary;
    if (!summary) {
        elements.detail.append(emptyPanel("Select an operations summary"));
        return;
    }
    const selectedId = state.selectedOperationId ?? "readiness";
    if (selectedId === "worker") {
        elements.detail.append(heading("Worker"), detailGrid([
            ["Type", summary.worker.workerType],
            ["Observed", summary.worker.observed ? "yes" : "no"],
            ["Status", summary.worker.status],
            ["Worker", summary.worker.workerId ?? ""],
            ["Stale", summary.worker.stale ? "yes" : "no"],
            ["Last seen", summary.worker.lastSeenAt ? shortDate(summary.worker.lastSeenAt) : ""],
            ["Last seen age", secondsText(summary.worker.lastSeenAgeSeconds)],
            ["Last success", summary.worker.lastSuccessAt ? shortDate(summary.worker.lastSuccessAt) : ""],
            ["Last error", summary.worker.lastError ?? ""]
        ]));
        return;
    }
    if (selectedId === "outbox") {
        elements.detail.append(heading("Outbox"), detailGrid([
            ["Ready pending", summary.outbox.readyPending.toString()],
            ["Delayed pending", summary.outbox.delayedPending.toString()],
            ["Processing", summary.outbox.processing.toString()],
            ["Dead letter", summary.outbox.deadLetter.toString()],
            ["Failed", summary.outbox.failed.toString()],
            ["Retrying failed", summary.outbox.retryingFailed.toString()],
            ["Expired processing", summary.outbox.expiredProcessing.toString()],
            ["Oldest ready pending", secondsText(summary.outbox.oldestReadyPendingSeconds)]
        ]));
        return;
    }
    if (selectedId === "retrieval") {
        elements.detail.append(heading("Retrieval feedback"), detailGrid([
            ["Window", `${summary.retrievalFeedback.windowHours.toFixed(1)} hours`],
            ["Total", summary.retrievalFeedback.total.toString()]
        ]), feedbackList(summary.retrievalFeedback.byType));
        return;
    }
    if (selectedId === "quality") {
        elements.detail.append(heading("Memory quality"), detailGrid([
            ["Window", `${summary.memoryQuality.windowHours.toFixed(1)} hours`],
            ["Durable memory items", summary.memoryQuality.durableMemoryItems.toString()],
            ["Active memory items", summary.memoryQuality.activeMemoryItems.toString()],
            ["Source-linked active items", summary.memoryQuality.sourceLinkedActiveMemoryItems.toString()],
            ["Source-link coverage", percentText(summary.memoryQuality.sourceLinkCoverage)],
            ["Stale memory items", summary.memoryQuality.staleMemoryItems.toString()],
            ["Stale memory rate", percentText(summary.memoryQuality.staleMemoryRate)],
            ["Useful feedback", summary.memoryQuality.usefulFeedbackTotal.toString()],
            ["Useful feedback rate", percentText(summary.memoryQuality.usefulFeedbackRate)],
            ["Missing reports", summary.memoryQuality.missingMemoryReports.toString()],
            ["Missing reports per hour", summary.memoryQuality.missingMemoryReportsPerHour.toFixed(3)],
            ["Role-boundary misses", summary.memoryQuality.roleBoundaryMisses.toString()],
            ["Role-boundary disclosed items", summary.memoryQuality.roleBoundaryMissDisclosedItems.toString()],
            ["Duplicate groups", summary.memoryQuality.duplicateCandidateGroups.toString()],
            ["Duplicate items", summary.memoryQuality.duplicateCandidateItems.toString()],
            ["Duplicate ratio", percentText(summary.memoryQuality.duplicateRatio)]
        ]));
        return;
    }
    if (selectedId === "reviews") {
        elements.detail.append(heading("Reviews and exports"), detailGrid([
            ["Pending reviews", summary.reviews.pending.toString()],
            ["Stale vault exports", summary.vaultExports.stale.toString()]
        ]), actionRow([
            linkAction("Open reviews", "/reviews/"),
            linkAction("Open memory", "/admin/")
        ]));
        return;
    }
    if (selectedId === "context") {
        elements.detail.append(heading("Context product"), detailGrid([
            ["Packets", summary.contextProduct.runtime.packetCount.toString()],
            ["Items", summary.contextProduct.runtime.itemCount.toString()],
            ["Explained items", summary.contextProduct.runtime.explainedItemCount.toString()],
            ["Feedback actions", summary.contextProduct.runtime.feedbackActionCount.toString()],
            ["Explanation coverage", percentText(summary.contextProduct.runtime.explanationCoverage)],
            ["Benchmark observed", summary.contextProduct.benchmark.observed ? "yes" : "no"],
            ["Benchmark source", summary.contextProduct.benchmark.source],
            ["Benchmark error", summary.contextProduct.benchmark.readError ?? ""]
        ]), feedbackList(summary.contextProduct.feedbackActions.byAction));
        return;
    }
    if (selectedId === "embeddings") {
        elements.detail.append(heading("Embedding index failures"), detailGrid([
            ["Retrying failed", summary.embeddingFailures.retryingFailed.toString()],
            ["Dead letter", summary.embeddingFailures.deadLetter.toString()],
            ["Failed", summary.embeddingFailures.failed.toString()],
            ["Expired processing", summary.embeddingFailures.expiredProcessing.toString()]
        ]));
        return;
    }
    elements.detail.append(heading("Readiness"), detailGrid([
        ["Status", summary.status],
        ["API", summary.api.status],
        ["Generated", shortDate(summary.generatedAt)]
    ]));
}
function renderMemoryDetail() {
    const fact = selectedFact();
    if (!fact) {
        elements.detail.append(emptyPanel("Select a memory fact"));
        return;
    }
    const object = fact.policy.contentVisible
        ? fact.object ?? ""
        : contentHiddenText(fact.policy.contentVisibilityReason);
    const rows = [
        ["Memory", fact.id],
        ["Status", fact.status],
        ["Scope", `${fact.scopeType}:${fact.scopeId}`],
        ["Namespace", fact.namespace],
        ["Type", fact.memoryType],
        ["Visibility", fact.visibility],
        ["Predicate", fact.predicate ?? contentHiddenText(fact.policy.contentVisibilityReason)],
        ["Confidence", fact.confidence.toFixed(3)],
        ["Trust", fact.trustLevel],
        ["Source", fact.sourceLink],
        ["Source retention", fact.policy.sourceRetentionClass],
        ["Source sensitivity", fact.policy.sourceSensitivity],
        ["Source trust", fact.policy.sourceTrustLevel],
        ["Source redaction", fact.policy.sourceRedactionStatus],
        ["Proposed by", fact.proposedByPrincipalId ?? ""],
        ["Updated", shortDate(fact.updatedAt)]
    ];
    const openButton = document.createElement("button");
    openButton.type = "button";
    openButton.className = "primary-action";
    openButton.textContent = "Open source";
    openButton.disabled = state.busy;
    openButton.addEventListener("click", () => void openSource(fact.sourceLink));
    const objectClass = fact.policy.contentVisible ? "memory-object" : "memory-object hidden-content";
    elements.detail.append(heading(displaySubject(fact)), paragraph(object, objectClass), detailGrid(rows), actionRow([
        openButton,
        linkAction("Open reviews", "/reviews/")
    ]));
}
function renderEventDetail() {
    const sourceEvent = selectedEvent();
    if (!sourceEvent) {
        elements.detail.append(emptyPanel("Select a source event"));
        return;
    }
    const rows = [
        ["Event", sourceEvent.id],
        ["Type", sourceEvent.eventType],
        ["Scope", `${sourceEvent.scope.scopeType}:${sourceEvent.scope.scopeId}`],
        ["Retention", sourceEvent.retentionClass],
        ["Sensitivity", sourceEvent.sensitivity],
        ["Trust", sourceEvent.trustLevel],
        ["Redaction", sourceEvent.redactionStatus],
        ["Redacted", sourceEvent.redactedAt ? shortDate(sourceEvent.redactedAt) : ""],
        ["Redaction event", sourceEvent.redactionEventLink ?? ""],
        ["Hash", sourceEvent.contentHash ?? ""],
        ["External payload", sourceEvent.externalPayloadUri ?? ""],
        ["Principal", sourceEvent.principalId ?? ""],
        ["Agent", sourceEvent.agentPrincipalId ?? ""],
        ["Role", sourceEvent.roleId ?? sourceEvent.scope.roleId ?? ""],
        ["Created", shortDate(sourceEvent.createdAt)]
    ];
    elements.detail.append(heading(sourceEvent.eventType), paragraph(sourceEvent.policy.contentVisibilityReason, "memory-object hidden-content"), detailGrid(rows), referenceList(sourceEvent.references));
    if (canOpenSourceEvent(sourceEvent)) {
        const openButton = document.createElement("button");
        openButton.type = "button";
        openButton.className = "primary-action";
        openButton.textContent = "Open payload";
        openButton.disabled = state.busy;
        openButton.addEventListener("click", () => void openSource(sourceEvent.sourceLink));
        elements.detail.append(actionRow([
            openButton,
            linkAction("Open reviews", "/reviews/")
        ]));
    }
    else {
        elements.detail.append(actionRow([
            linkAction("Open reviews", "/reviews/")
        ]));
    }
}
function renderSourceDetail() {
    elements.sourceDetail.replaceChildren();
    if (state.mode === "compliance") {
        const item = selectedComplianceItem();
        if (!item) {
            elements.sourceDetail.append(emptyPanel("No compliance check selected"));
            return;
        }
        elements.sourceDetail.append(linkList(item.links));
        return;
    }
    if (state.mode === "pilot") {
        const status = state.pilotReadiness;
        if (!status) {
            elements.sourceDetail.append(emptyPanel("No pilot readiness status"));
            return;
        }
        elements.sourceDetail.append(requiredGoInputList(status.requiredToFlipToGo), evidenceList(Object.values(status.canonicalDocuments), "Canonical Docs"), nextWorkList(status.nextRecommendedWork));
        return;
    }
    if (state.mode === "operations") {
        elements.sourceDetail.append(linkList([
            {
                label: "Operations summary",
                href: "/api/operations/summary",
                kind: "api",
                method: "GET"
            },
            {
                label: "Operations metrics",
                href: "/api/operations/metrics",
                kind: "api",
                method: "GET"
            },
            {
                label: "Readiness health",
                href: "/health/ready",
                kind: "api",
                method: "GET"
            },
            {
                label: "Review queue",
                href: "/reviews/",
                kind: "ui",
                method: "GET"
            }
        ]));
        return;
    }
    if (state.mode === "management") {
        renderManagementSourceDetail();
        return;
    }
    if (state.mode === "access") {
        if (!state.accessResult) {
            elements.sourceDetail.append(emptyPanel("No access result"));
            return;
        }
        const json = document.createElement("pre");
        json.className = "source-json";
        json.textContent = JSON.stringify(state.accessResult, null, 2);
        elements.sourceDetail.append(json);
        return;
    }
    if (state.mode === "registration") {
        renderRegistrationSourceDetail();
        return;
    }
    if (!state.selectedSource) {
        elements.sourceDetail.append(emptyPanel("No source opened"));
        return;
    }
    const source = state.selectedSource;
    const rows = [
        ["Event", source.id],
        ["Type", source.eventType],
        ["Scope", `${source.scope.scopeType}:${source.scope.scopeId}`],
        ["Retention", source.retentionClass],
        ["Sensitivity", source.sensitivity],
        ["Trust", source.trustLevel],
        ["Hash", source.contentHash ?? ""],
        ["Created", shortDate(source.createdAt)]
    ];
    const json = document.createElement("pre");
    json.className = "source-json";
    json.textContent = JSON.stringify(source.content, null, 2);
    elements.sourceDetail.append(heading("Source event"), detailGrid(rows), json);
}
function evidenceList(paths, title) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading(title));
    if (paths.length === 0) {
        section.append(emptyPanel("No evidence links"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const path of paths) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(path, "memory-title"), pillRow([path.endsWith(".md") ? "doc" : path.endsWith(".json") ? "json" : "code"]));
        list.append(row);
    }
    section.append(list);
    return section;
}
function missingInputList(inputs) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("Missing Inputs"));
    if (inputs.length === 0) {
        section.append(emptyPanel("No missing inputs"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const input of inputs) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(input, "memory-title"), pillRow(["required"]));
        list.append(row);
    }
    section.append(list);
    return section;
}
function requiredGoInputList(inputs) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("Required To GO"));
    if (inputs.length === 0) {
        section.append(emptyPanel("No required GO inputs"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const input of inputs) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(input, "memory-title"), pillRow(["go_input"]));
        list.append(row);
    }
    section.append(list);
    return section;
}
function nextWorkList(items) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("Next Work"));
    if (items.length === 0) {
        section.append(emptyPanel("No next work recorded"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const item of items) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(`${item.id} ${item.title}`, "memory-title"), line(item.uses, "memory-meta"));
        list.append(row);
    }
    section.append(list);
    return section;
}
function accessForm(title, fields, buttonText, onSubmit) {
    const form = document.createElement("form");
    form.className = "access-form";
    form.append(heading(title));
    const grid = document.createElement("div");
    grid.className = "access-grid";
    grid.append(...fields);
    const button = document.createElement("button");
    button.type = "submit";
    button.className = "primary-action";
    button.textContent = buttonText;
    button.disabled = state.busy;
    form.append(grid, button);
    form.addEventListener("submit", event => {
        event.preventDefault();
        void onSubmit(form);
    });
    return form;
}
function textField(name, labelText) {
    const label = document.createElement("label");
    const span = document.createElement("span");
    const input = document.createElement("input");
    span.textContent = labelText;
    input.name = name;
    input.autocomplete = "off";
    label.append(span, input);
    return label;
}
function dateTimeField(name, labelText) {
    const label = document.createElement("label");
    const span = document.createElement("span");
    const input = document.createElement("input");
    span.textContent = labelText;
    input.name = name;
    input.type = "datetime-local";
    label.append(span, input);
    return label;
}
function dateField(name, labelText) {
    const label = document.createElement("label");
    const span = document.createElement("span");
    const input = document.createElement("input");
    span.textContent = labelText;
    input.name = name;
    input.type = "date";
    label.append(span, input);
    return label;
}
function numberField(name, labelText, value) {
    const label = document.createElement("label");
    const span = document.createElement("span");
    const input = document.createElement("input");
    span.textContent = labelText;
    input.name = name;
    input.type = "number";
    input.min = "1";
    input.max = "5000";
    input.value = value;
    label.append(span, input);
    return label;
}
function selectField(name, labelText, values) {
    const label = document.createElement("label");
    const span = document.createElement("span");
    const select = document.createElement("select");
    span.textContent = labelText;
    select.name = name;
    for (const value of values) {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = value;
        select.append(option);
    }
    label.append(span, select);
    return label;
}
function formValue(form, name) {
    const value = new FormData(form).get(name);
    return typeof value === "string" ? value.trim() : "";
}
async function apiFetchText(path, init = {}) {
    const apiBase = elements.apiBase.value.trim().replace(/\/$/, "");
    const headers = new Headers(init.headers);
    applyAuth(headers);
    const response = await fetch(`${apiBase}${path}`, {
        ...init,
        credentials: "same-origin",
        headers
    });
    const text = await response.text();
    if (!response.ok) {
        let detail = response.statusText;
        try {
            const payload = text ? JSON.parse(text) : null;
            detail = payload?.detail ?? payload?.title ?? detail;
        }
        catch {
            detail = text || detail;
        }
        throw new ApiFetchError(response.status, detail);
    }
    return {
        text,
        headers: response.headers
    };
}
async function postAccess(path, body) {
    setBusy(true);
    setStatus("Saving");
    try {
        state.accessResult = await apiFetch(path, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(body)
        });
        setStatus("Saved");
    }
    catch (error) {
        state.accessResult = { error: errorMessage(error) };
        setStatus("Error");
    }
    finally {
        setBusy(false);
        render();
    }
}
async function exportAudit(form) {
    setBusy(true);
    setStatus("Exporting");
    const actionType = formValue(form, "actionType");
    const outcome = formValue(form, "outcome");
    const body = {
        occurredFrom: dateTimeValue(form, "occurredFrom"),
        occurredTo: dateTimeValue(form, "occurredTo"),
        scopeType: formValue(form, "scopeType"),
        scopeId: formValue(form, "scopeId"),
        limit: Number(formValue(form, "limit") || "1000")
    };
    if (actionType !== "all") {
        body.actionTypes = [actionType];
    }
    if (outcome !== "all") {
        body.outcomes = [outcome];
    }
    try {
        const response = await apiFetchText("/api/admin/audit-exports", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(body)
        });
        const firstLine = response.text.split("\n", 1)[0] || "{}";
        const manifest = JSON.parse(firstLine);
        const fileName = auditExportFileName(response.headers, manifest.exportId);
        downloadText(fileName, response.text, "application/x-ndjson");
        state.accessResult = {
            manifest,
            fileName
        };
        setStatus("Exported");
    }
    catch (error) {
        state.accessResult = { error: errorMessage(error) };
        setStatus("Error");
    }
    finally {
        setBusy(false);
        render();
    }
}
function dateTimeValue(form, name) {
    const value = formValue(form, name);
    return value ? new Date(value).toISOString() : null;
}
function auditExportFileName(headers, exportId) {
    const disposition = headers.get("content-disposition") ?? "";
    const match = /filename="([^"]+)"/i.exec(disposition);
    if (match) {
        return match[1];
    }
    return typeof exportId === "string" && exportId
        ? `access-audit-${exportId.replaceAll("-", "")}.ndjson`
        : "access-audit.ndjson";
}
function downloadText(fileName, text, type) {
    const blob = new Blob([text], { type });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}
function metricList(metrics) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("Metrics"));
    if (metrics.length === 0) {
        section.append(emptyPanel("No dedicated metric names"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const metric of metrics) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(metric.name, "memory-title"), line(metric.description, "memory-meta"));
        list.append(row);
    }
    section.append(list);
    return section;
}
function linkList(links) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("Links"));
    if (links.length === 0) {
        section.append(emptyPanel("No evidence links"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const link of links) {
        const row = document.createElement("div");
        row.className = "audit-row";
        const target = document.createElement(link.method === "GET" ? "a" : "span");
        target.className = "memory-meta";
        target.textContent = link.href;
        if (target instanceof HTMLAnchorElement) {
            target.href = apiUrl(link.href);
            target.target = "_blank";
            target.rel = "noreferrer";
        }
        row.append(line(link.label, "memory-title"), pillRow([link.method, link.kind]), target);
        list.append(row);
    }
    section.append(list);
    return section;
}
function apiUrl(path) {
    return `${elements.apiBase.value.trim().replace(/\/$/, "")}${path}`;
}
function referenceList(references) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("References"));
    if (references.length === 0) {
        section.append(emptyPanel("No linked records"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const reference of references) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(reference.referenceType, "memory-title"), pillRow([reference.status]), line(reference.id, "memory-meta"), line(reference.label ?? reference.targetType ?? "", "memory-date"));
        list.append(row);
    }
    section.append(list);
    return section;
}
function operationListItems(summary) {
    const outboxRisk = summary.outbox.deadLetter + summary.outbox.failed + summary.outbox.retryingFailed + summary.outbox.expiredProcessing;
    const embeddingRisk = summary.embeddingFailures.deadLetter
        + summary.embeddingFailures.failed
        + summary.embeddingFailures.retryingFailed
        + summary.embeddingFailures.expiredProcessing;
    return [
        {
            id: "readiness",
            title: "Readiness",
            pills: [summary.status, summary.api.status],
            summary: `Generated ${shortDate(summary.generatedAt)}`,
            detail: "/api/operations/summary"
        },
        {
            id: "worker",
            title: "Worker",
            pills: [summary.worker.status, summary.worker.stale ? "stale" : "fresh"],
            summary: summary.worker.observed ? summary.worker.workerType : "No heartbeat observed",
            detail: summary.worker.lastSeenAgeSeconds === null ? "" : secondsText(summary.worker.lastSeenAgeSeconds)
        },
        {
            id: "outbox",
            title: "Outbox",
            pills: [outboxRisk > 0 ? "needs_review" : "clear"],
            summary: `${summary.outbox.readyPending} ready, ${summary.outbox.processing} processing`,
            detail: `${outboxRisk} failure signals`
        },
        {
            id: "retrieval",
            title: "Retrieval feedback",
            pills: [summary.retrievalFeedback.total > 0 ? "observed" : "quiet"],
            summary: `${summary.retrievalFeedback.total} actions in ${summary.retrievalFeedback.windowHours.toFixed(0)}h`,
            detail: topFeedbackLabel(summary.retrievalFeedback.byType)
        },
        {
            id: "quality",
            title: "Memory quality",
            pills: [summary.memoryQuality.missingMemoryReports > 0 || summary.memoryQuality.duplicateCandidateGroups > 0 ? "needs_review" : "clear"],
            summary: `${percentText(summary.memoryQuality.sourceLinkCoverage)} source coverage`,
            detail: `${summary.memoryQuality.missingMemoryReports} missing reports`
        },
        {
            id: "reviews",
            title: "Reviews and exports",
            pills: [summary.reviews.pending > 0 ? "needs_review" : "clear"],
            summary: `${summary.reviews.pending} pending reviews`,
            detail: `${summary.vaultExports.stale} stale vault exports`
        },
        {
            id: "context",
            title: "Context product",
            pills: [summary.contextProduct.benchmark.observed ? "benchmark" : "no_benchmark"],
            summary: `${summary.contextProduct.runtime.packetCount} packets`,
            detail: `${percentText(summary.contextProduct.runtime.explanationCoverage)} explanation coverage`
        },
        {
            id: "embeddings",
            title: "Embeddings",
            pills: [embeddingRisk > 0 ? "needs_review" : "clear"],
            summary: `${summary.embeddingFailures.retryingFailed} retrying, ${summary.embeddingFailures.deadLetter} dead-letter`,
            detail: `${embeddingRisk} failure signals`
        }
    ];
}
function feedbackList(items) {
    const section = document.createElement("section");
    section.className = "audit-section";
    section.append(heading("Feedback Types"));
    if (items.length === 0) {
        section.append(emptyPanel("No feedback rows"));
        return section;
    }
    const list = document.createElement("div");
    list.className = "audit-list";
    for (const item of items) {
        const row = document.createElement("div");
        row.className = "audit-row";
        row.append(line(item.feedbackType, "memory-title"), pillRow([item.count > 0 ? "observed" : "quiet"]), line(`${item.count} total`, "memory-meta"), line(`${percentText(item.share)} share · ${item.perHour.toFixed(2)}/hour`, "memory-date"));
        list.append(row);
    }
    section.append(list);
    return section;
}
function actionRow(actions) {
    const row = document.createElement("div");
    row.className = "action-row";
    row.append(...actions);
    return row;
}
function linkAction(label, href) {
    const link = document.createElement("a");
    link.className = "secondary-action";
    link.href = href;
    link.textContent = label;
    return link;
}
function topFeedbackLabel(items) {
    const top = [...items].sort((left, right) => right.count - left.count)[0];
    return top && top.count > 0 ? `${top.feedbackType} leads` : "No feedback yet";
}
function secondsText(value) {
    if (value === null) {
        return "";
    }
    if (value < 60) {
        return `${value.toFixed(0)}s`;
    }
    if (value < 3600) {
        return `${(value / 60).toFixed(1)}m`;
    }
    return `${(value / 3600).toFixed(1)}h`;
}
function percentText(value) {
    return `${(value * 100).toFixed(1)}%`;
}
function selectedFact() {
    return state.facts.find(fact => fact.id === state.selectedFactId) ?? null;
}
function selectedEvent() {
    return state.events.find(sourceEvent => sourceEvent.id === state.selectedEventId) ?? null;
}
function selectedComplianceItem() {
    return state.complianceStatus?.items.find(item => item.id === state.selectedComplianceId) ?? null;
}
function selectedPilotGate() {
    return state.pilotReadiness?.gates.find(gate => gate.id === state.selectedPilotGateId) ?? null;
}
function setBusy(busy) {
    state.busy = busy;
    elements.refresh.disabled = busy;
}
function setStatus(value) {
    elements.status.textContent = value;
}
function displaySubject(fact) {
    return fact.policy.contentVisible
        ? fact.subject ?? "(untitled memory)"
        : contentHiddenText(fact.policy.contentVisibilityReason);
}
function contentHiddenText(reason) {
    return reason === "memory_content_hidden_by_source_policy"
        ? "[content hidden by source policy]"
        : "[content hidden by lifecycle]";
}
function canOpenSourceEvent(sourceEvent) {
    return sourceEvent.retentionClass !== "erasure_requested"
        && sourceEvent.redactionStatus === "none";
}
function errorMessage(error) {
    return error instanceof Error ? error.message : String(error);
}
function shortDate(value) {
    return new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
    }).format(new Date(value));
}
function line(value, className) {
    const element = document.createElement("span");
    element.className = className;
    element.textContent = value;
    return element;
}
function heading(value) {
    const element = document.createElement("h2");
    element.textContent = value;
    return element;
}
function paragraph(value, className) {
    const element = document.createElement("p");
    element.className = className;
    element.textContent = value;
    return element;
}
function pillRow(values) {
    const row = document.createElement("span");
    row.className = "memory-status";
    for (const value of values) {
        if (!value) {
            continue;
        }
        const pill = document.createElement("span");
        pill.className = `pill ${value}`;
        pill.textContent = value;
        row.append(pill);
    }
    return row;
}
function detailGrid(rows) {
    const grid = document.createElement("dl");
    grid.className = "detail-grid";
    for (const [label, value] of rows) {
        const term = document.createElement("dt");
        const description = document.createElement("dd");
        term.textContent = label;
        description.textContent = value;
        grid.append(term, description);
    }
    return grid;
}
function emptyPanel(value) {
    const panel = document.createElement("div");
    panel.className = "empty";
    panel.textContent = value;
    return panel;
}
function updateFilterVisibility() {
    for (const element of document.querySelectorAll("[data-event-filter]")) {
        element.hidden = state.mode !== "events";
    }
    for (const element of document.querySelectorAll("[data-memory-filter]")) {
        element.hidden = state.mode !== "memory";
    }
    for (const element of document.querySelectorAll("[data-management-filter]")) {
        element.hidden = state.mode !== "management";
    }
    for (const element of document.querySelectorAll("[data-role-filter]")) {
        element.hidden = state.mode !== "memory" && state.mode !== "events";
    }
    elements.statusFilter.closest("label").hidden = state.mode !== "memory";
    elements.scopeTypeFilter.closest("label").hidden = state.mode === "access" || state.mode === "registration" || state.mode === "compliance" || state.mode === "pilot" || state.mode === "operations" || state.mode === "management";
    elements.scopeIdFilter.closest("label").hidden = state.mode === "access" || state.mode === "registration" || state.mode === "compliance" || state.mode === "pilot" || state.mode === "operations" || state.mode === "management";
    elements.query.closest("label").hidden = state.mode === "access" || state.mode === "registration" || state.mode === "compliance" || state.mode === "pilot" || state.mode === "operations";
}
