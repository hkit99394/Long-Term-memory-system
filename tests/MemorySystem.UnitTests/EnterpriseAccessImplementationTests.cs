namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Enterprise_access_foundation_contract_is_documented_and_guarded()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "026_identity_bindings.sql"));
        var accessAuditMigration = File.ReadAllText(Path.Combine(root, "migrations", "027_access_audit_events.sql"));
        var restoreManifest = File.ReadAllText(Path.Combine(root, "scripts", "restore-validation-tables.txt"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Authentication", "PostgresIdentityBindingStore.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "IIdentityBindingStore.cs"));
        var lookup = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "IdentityBindingLookup.cs"));
        var statuses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "IdentityBindingStatuses.cs"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var principalResolver = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "IPrincipalResolver.cs"));
        var authenticatedPrincipal = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "AuthenticatedPrincipal.cs"));
        var apiKeyResolutionRequest = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "ApiKeyPrincipalResolutionRequest.cs"));
        var authenticationMethods = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "AuthenticationMethods.cs"));
        var claimTypes = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Authentication", "MemorySystemClaimTypes.cs"));
        var postgresPrincipalResolver = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Authentication", "PostgresPrincipalResolver.cs"));
        var authHandler = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "ApiKeyAuthenticationHandler.cs"));
        var authRegistration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "ApiAuthenticationServiceCollectionExtensions.cs"));
        var oidcHandler = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "OidcAuthenticationHandler.cs"));
        var oidcOptions = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "OidcAuthenticationOptions.cs"));
        var oidcJwtValidator = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "OidcJwtValidator.cs"));
        var oidcJwksProvider = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "IOidcJwksProvider.cs"));
        var httpOidcJwksProvider = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Authentication", "HttpOidcJwksProvider.cs"));
        var accessAuditStoreContract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "AccessAuditing", "IAccessAuditEventStore.cs"));
        var accessAuditCommand = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "AccessAuditing", "AccessAuditEventCommand.cs"));
        var accessAuditActions = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "AccessAuditing", "AccessAuditActionTypes.cs"));
        var accessAuditOutcomes = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "AccessAuditing", "AccessAuditOutcomes.cs"));
        var postgresAccessAuditStore = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "AccessAuditing", "PostgresAccessAuditEventStore.cs"));
        var accessRegistration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Access", "ApiAccessServiceCollectionExtensions.cs"));

        Assert.Contains("CREATE TABLE identity_bindings", migration, StringComparison.Ordinal);
        Assert.Contains("provider TEXT NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("issuer TEXT NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("subject TEXT NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("principal_id UUID NOT NULL REFERENCES principals(id)", migration, StringComparison.Ordinal);
        Assert.Contains("status TEXT NOT NULL DEFAULT 'active'", migration, StringComparison.Ordinal);
        Assert.Contains("external_display_name TEXT", migration, StringComparison.Ordinal);
        Assert.Contains("external_email TEXT", migration, StringComparison.Ordinal);
        Assert.Contains("external_tenant_id TEXT", migration, StringComparison.Ordinal);
        Assert.Contains("provider_metadata JSONB NOT NULL DEFAULT '{}'::jsonb", migration, StringComparison.Ordinal);
        Assert.Contains("last_seen_at TIMESTAMPTZ", migration, StringComparison.Ordinal);
        Assert.Contains("CHECK (status IN ('active', 'disabled', 'deleted'))", migration, StringComparison.Ordinal);
        Assert.Contains("ux_identity_bindings_active_subject", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE status = 'active'", migration, StringComparison.Ordinal);
        Assert.Contains("trg_identity_bindings_set_updated_at", migration, StringComparison.Ordinal);

        Assert.Contains("identity_bindings", restoreManifest, StringComparison.Ordinal);
        Assert.Contains("access_audit_events", restoreManifest, StringComparison.Ordinal);

        Assert.Contains("IIdentityBindingStore", contract, StringComparison.Ordinal);
        Assert.Contains("IdentityBindingLookup", lookup, StringComparison.Ordinal);
        Assert.Contains("public const string Active = \"active\"", statuses, StringComparison.Ordinal);
        Assert.Contains("public const string Disabled = \"disabled\"", statuses, StringComparison.Ordinal);
        Assert.Contains("public const string Deleted = \"deleted\"", statuses, StringComparison.Ordinal);
        Assert.Contains("binding.status = 'active'", store, StringComparison.Ordinal);
        Assert.Contains("principal.status = 'active'", store, StringComparison.Ordinal);

        Assert.Contains("| EA-01 | P0 | Done | Add identity-binding schema.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-02 | P0 | Done | Introduce shared principal resolution.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-03 | P0 | Done | Add access audit event model.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-04 | P0 | Done | Add generic OIDC authentication.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-01 | P0 | Done | Add identity-binding schema.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("| EA-03 | P0 | Done | Add access audit event model.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("| EA-04 | P0 | Done | Add generic OIDC authentication.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);

        Assert.Contains("IPrincipalResolver", principalResolver, StringComparison.Ordinal);
        Assert.Contains("ResolveApiKeyAsync", principalResolver, StringComparison.Ordinal);
        Assert.Contains("ResolveIdentityBindingAsync", principalResolver, StringComparison.Ordinal);
        Assert.Contains("AuthenticatedPrincipal", authenticatedPrincipal, StringComparison.Ordinal);
        Assert.Contains("PrincipalType", authenticatedPrincipal, StringComparison.Ordinal);
        Assert.Contains("AuthMethod", authenticatedPrincipal, StringComparison.Ordinal);
        Assert.Contains("CredentialId", authenticatedPrincipal, StringComparison.Ordinal);
        Assert.Contains("ApiKeyPrincipalResolutionRequest", apiKeyResolutionRequest, StringComparison.Ordinal);
        Assert.Contains("public const string ApiKey = \"api_key\"", authenticationMethods, StringComparison.Ordinal);
        Assert.Contains("public const string Oidc = \"oidc\"", authenticationMethods, StringComparison.Ordinal);
        Assert.Contains("public const string PrincipalId = \"memory_system_principal_id\"", claimTypes, StringComparison.Ordinal);
        Assert.Contains("public const string PrincipalType = \"memory_system_principal_type\"", claimTypes, StringComparison.Ordinal);
        Assert.Contains("public const string AuthMethod = \"memory_system_auth_method\"", claimTypes, StringComparison.Ordinal);
        Assert.Contains("public const string CredentialId = \"memory_system_credential_id\"", claimTypes, StringComparison.Ordinal);
        Assert.Contains("principal_type", postgresPrincipalResolver, StringComparison.Ordinal);
        Assert.Contains("status = 'active'", postgresPrincipalResolver, StringComparison.Ordinal);
        Assert.Contains("identityBindingStore.FindActiveAsync", postgresPrincipalResolver, StringComparison.Ordinal);
        Assert.Contains("IPrincipalResolver", authHandler, StringComparison.Ordinal);
        Assert.Contains("ApiKeyPrincipalResolutionRequest", authHandler, StringComparison.Ordinal);
        Assert.Contains("MemorySystemClaimTypes.AuthMethod", authHandler, StringComparison.Ordinal);
        Assert.Contains("MemorySystemClaimTypes.CredentialId", authHandler, StringComparison.Ordinal);
        Assert.Contains("IPrincipalResolver, PostgresPrincipalResolver", authRegistration, StringComparison.Ordinal);
        Assert.Contains("IOidcJwksProvider, HttpOidcJwksProvider", authRegistration, StringComparison.Ordinal);
        Assert.Contains("OidcJwtValidator", authRegistration, StringComparison.Ordinal);
        Assert.Contains("OidcAuthenticationOptions, OidcAuthenticationHandler", authRegistration, StringComparison.Ordinal);
        Assert.Contains("OidcAuthenticationDefaults.AuthenticationScheme", authRegistration, StringComparison.Ordinal);
        Assert.Contains("options.DefaultPolicy = authenticatedApiPolicy", authRegistration, StringComparison.Ordinal);
        Assert.Contains("options.FallbackPolicy = authenticatedApiPolicy", authRegistration, StringComparison.Ordinal);

        Assert.Contains("OidcAuthenticationHandler", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("jwtValidator.ValidateAsync", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("ResolveIdentityBindingAsync", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("IdentityBindingLookup", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("OidcAuthenticationDefaults.Provider", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("AuthenticationMethods.Oidc", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("resolvedPrincipal.PrincipalType, \"human\"", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("MemorySystemClaimTypes.ExternalIssuer", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("MemorySystemClaimTypes.ExternalSubject", oidcHandler, StringComparison.Ordinal);
        Assert.Contains("HasRequiredConfiguration", oidcOptions, StringComparison.Ordinal);
        Assert.Contains("HasHttpsMetadataWhenRequired", oidcOptions, StringComparison.Ordinal);
        Assert.Contains("ClockSkewSeconds", oidcOptions, StringComparison.Ordinal);
        Assert.Contains("IOidcJwksProvider", oidcJwksProvider, StringComparison.Ordinal);
        Assert.Contains("GetJwksAsync", oidcJwksProvider, StringComparison.Ordinal);
        Assert.Contains("keys", httpOidcJwksProvider, StringComparison.Ordinal);
        Assert.Contains("RS256", oidcJwtValidator, StringComparison.Ordinal);
        Assert.Contains("\"iss\"", oidcJwtValidator, StringComparison.Ordinal);
        Assert.Contains("\"aud\"", oidcJwtValidator, StringComparison.Ordinal);
        Assert.Contains("\"sub\"", oidcJwtValidator, StringComparison.Ordinal);
        Assert.Contains("\"exp\"", oidcJwtValidator, StringComparison.Ordinal);
        Assert.Contains("\"nbf\"", oidcJwtValidator, StringComparison.Ordinal);

        Assert.Contains("CREATE TABLE access_audit_events", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("authentication", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("authorization_denied", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("organization_membership_change", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("project_membership_change", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("role_assignment_change", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("namespace_grant_change", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("service_credential_change", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("audit_export", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("auth_method TEXT", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("credential_id TEXT", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("identity_binding_id UUID REFERENCES identity_bindings(id)", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("audit_metadata JSONB NOT NULL DEFAULT '{}'::jsonb", accessAuditMigration, StringComparison.Ordinal);
        Assert.Contains("NOT (audit_metadata ?| ARRAY", accessAuditMigration, StringComparison.Ordinal);

        Assert.Contains("IAccessAuditEventStore", accessAuditStoreContract, StringComparison.Ordinal);
        Assert.Contains("RecordAsync", accessAuditStoreContract, StringComparison.Ordinal);
        Assert.Contains("AccessAuditEventCommand", accessAuditCommand, StringComparison.Ordinal);
        Assert.Contains("AuthMethod", accessAuditCommand, StringComparison.Ordinal);
        Assert.Contains("CredentialId", accessAuditCommand, StringComparison.Ordinal);
        Assert.Contains("Metadata", accessAuditCommand, StringComparison.Ordinal);
        Assert.Contains("AuthorizationDenied", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("OrganizationMembershipChange", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("ProjectMembershipChange", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("RoleAssignmentChange", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("NamespaceGrantChange", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("ServiceCredentialChange", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("AuditExport", accessAuditActions, StringComparison.Ordinal);
        Assert.Contains("public const string Succeeded = \"succeeded\"", accessAuditOutcomes, StringComparison.Ordinal);
        Assert.Contains("public const string Failed = \"failed\"", accessAuditOutcomes, StringComparison.Ordinal);
        Assert.Contains("public const string Denied = \"denied\"", accessAuditOutcomes, StringComparison.Ordinal);
        Assert.Contains("ForbiddenMetadataKeys", postgresAccessAuditStore, StringComparison.Ordinal);
        Assert.Contains("rawPayload", postgresAccessAuditStore, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO access_audit_events", postgresAccessAuditStore, StringComparison.Ordinal);
        Assert.Contains("PostgresAccessAuditEventStore", accessRegistration, StringComparison.Ordinal);
        Assert.Contains("IAccessAuditEventStore", accessRegistration, StringComparison.Ordinal);
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
