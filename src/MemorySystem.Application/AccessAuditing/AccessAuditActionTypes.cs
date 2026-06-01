namespace MemorySystem.Application.AccessAuditing;

public static class AccessAuditActionTypes
{
    public const string Authentication = "authentication";
    public const string AuthorizationDenied = "authorization_denied";
    public const string PrincipalChange = "principal_change";
    public const string IdentityBindingChange = "identity_binding_change";
    public const string OrganizationMembershipChange = "organization_membership_change";
    public const string ProjectMembershipChange = "project_membership_change";
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
