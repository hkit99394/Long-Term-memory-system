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

function renderAccessDetail(): void {
  const panel = document.createElement("section");
  panel.className = "access-forms";

  panel.append(
    heading("Access management"),
    accessForm(
      "Organization membership",
      [
        textField("orgId", "Org ID"),
        textField("principalId", "Principal ID"),
        selectField("accessLevel", "Access", ["reader", "contributor", "reviewer", "admin", "owner"])
      ],
      "Save",
      form => postAccess("/api/admin/access/organization-memberships", {
        orgId: formValue(form, "orgId"),
        principalId: formValue(form, "principalId"),
        accessLevel: formValue(form, "accessLevel")
      })),
    accessForm(
      "Project membership",
      [
        textField("projectId", "Project ID"),
        textField("principalId", "Principal ID"),
        selectField("accessLevel", "Access", ["reader", "contributor", "reviewer", "admin"])
      ],
      "Save",
      form => postAccess("/api/admin/access/project-memberships", {
        projectId: formValue(form, "projectId"),
        principalId: formValue(form, "principalId"),
        accessLevel: formValue(form, "accessLevel")
      })),
    accessForm(
      "Project role definition",
      [
        textField("projectId", "Project ID"),
        textField("roleId", "Role ID"),
        textField("displayName", "Display name"),
        textField("description", "Description"),
        selectField("templateRoleId", "Template role", ["", ...defaultRoleIds]),
        selectField("status", "Status", ["active", "disabled"])
      ],
      "Save",
      form => {
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
      }),
    accessForm(
      "Role assignment",
      [
        textField("principalId", "Principal ID"),
        textField("roleId", "Role ID"),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID")
      ],
      "Assign",
      form => postAccess("/api/admin/access/role-assignments", {
        principalId: formValue(form, "principalId"),
        roleId: formValue(form, "roleId"),
        scopeType: formValue(form, "scopeType"),
        scopeId: formValue(form, "scopeId")
      })),
    accessForm(
      "Namespace grant",
      [
        selectField("targetType", "Target", ["principal", "role"]),
        textField("targetId", "Target ID"),
        textField("namespacePrefix", "Namespace"),
        selectField("permission", "Permission", ["read", "write", "review", "admin"]),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID")
      ],
      "Grant",
      form => {
        const targetType = formValue(form, "targetType");
        return postAccess("/api/admin/access/namespace-grants", {
          principalId: targetType === "principal" ? formValue(form, "targetId") : null,
          roleId: targetType === "role" ? formValue(form, "targetId") : null,
          namespacePrefix: formValue(form, "namespacePrefix"),
          permission: formValue(form, "permission"),
          scopeType: formValue(form, "scopeType"),
          scopeId: formValue(form, "scopeId")
        });
      }),
    accessForm(
      "Effective preview",
      [
        textField("principalId", "Principal ID"),
        selectField("permission", "Permission", ["read", "write", "review", "admin"]),
        selectField("scopeType", "Scope", ["project", "org"]),
        textField("scopeId", "Scope ID"),
        textField("namespacePrefix", "Namespace")
      ],
      "Preview",
      form => postAccess("/api/admin/access/effective-preview", {
        principalId: formValue(form, "principalId"),
        permission: formValue(form, "permission"),
        scopeType: formValue(form, "scopeType"),
        scopeId: formValue(form, "scopeId"),
        namespacePrefix: formValue(form, "namespacePrefix") || null
      })),
    accessForm(
      "Audit export",
      [
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
      ],
      "Export",
      form => exportAudit(form)));

  elements.detail.append(panel);
}
