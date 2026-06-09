namespace MemorySystem.Application.AccessAuditing;

public static class AccessAuditActionTypes
{
    public const string Authentication = "authentication";
    public const string AuthorizationDenied = "authorization_denied";
    public const string PrincipalChange = "principal_change";
    public const string IdentityBindingChange = "identity_binding_change";
    public const string OrganizationMembershipChange = "organization_membership_change";
    public const string ProjectMembershipChange = "project_membership_change";
    public const string ProjectRegistration = "project_registration";
    public const string ProjectLifecycleChange = "project_lifecycle_change";
    public const string ProjectScopeSettingsChange = "project_scope_settings_change";
    public const string ProjectRoleDefinitionChange = "project_role_definition_change";
    public const string RoleAssignmentChange = "role_assignment_change";
    public const string NamespaceGrantChange = "namespace_grant_change";
    public const string ServiceCredentialChange = "service_credential_change";
    public const string AuditExport = "audit_export";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Authentication,
        AuthorizationDenied,
        PrincipalChange,
        IdentityBindingChange,
        OrganizationMembershipChange,
        ProjectMembershipChange,
        ProjectRegistration,
        ProjectLifecycleChange,
        ProjectScopeSettingsChange,
        ProjectRoleDefinitionChange,
        RoleAssignmentChange,
        NamespaceGrantChange,
        ServiceCredentialChange,
        AuditExport
    };

    public static bool IsSupported(string? actionType)
    {
        return !string.IsNullOrWhiteSpace(actionType) && All.Contains(actionType);
    }
}
