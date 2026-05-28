namespace MemorySystem.Application.Access;

public static class MemoryAccessPolicy
{
    public static bool HasRequiredAccessLevel(string? accessLevel, string permission, bool allowOwner)
    {
        if (string.IsNullOrWhiteSpace(accessLevel))
        {
            return false;
        }

        return permission switch
        {
            MemoryAccessPermissions.Read => accessLevel is "reader" or "contributor" or "reviewer" or "admin"
                || (allowOwner && accessLevel == "owner"),
            MemoryAccessPermissions.Write => accessLevel is "contributor" or "reviewer" or "admin"
                || (allowOwner && accessLevel == "owner"),
            MemoryAccessPermissions.Review => accessLevel is "reviewer" or "admin"
                || (allowOwner && accessLevel == "owner"),
            MemoryAccessPermissions.Admin => accessLevel is "admin"
                || (allowOwner && accessLevel == "owner"),
            _ => false
        };
    }

    public static string[] GrantPermissionsFor(string permission)
    {
        return permission switch
        {
            MemoryAccessPermissions.Read =>
            [
                MemoryAccessPermissions.Read,
                MemoryAccessPermissions.Write,
                MemoryAccessPermissions.Review,
                MemoryAccessPermissions.Admin
            ],
            MemoryAccessPermissions.Write =>
            [
                MemoryAccessPermissions.Write,
                MemoryAccessPermissions.Admin
            ],
            MemoryAccessPermissions.Review =>
            [
                MemoryAccessPermissions.Review,
                MemoryAccessPermissions.Admin
            ],
            MemoryAccessPermissions.Admin => [MemoryAccessPermissions.Admin],
            _ => [permission]
        };
    }

    public static string[] ProjectAccessLevelsFor(string permission)
    {
        return permission switch
        {
            MemoryAccessPermissions.Read =>
            [
                "reader",
                "contributor",
                "reviewer",
                "admin"
            ],
            MemoryAccessPermissions.Write =>
            [
                "contributor",
                "reviewer",
                "admin"
            ],
            MemoryAccessPermissions.Review =>
            [
                "reviewer",
                "admin"
            ],
            MemoryAccessPermissions.Admin => ["admin"],
            _ => []
        };
    }

    public static string[] OrganizationAccessLevelsFor(string permission, bool allowOwner)
    {
        var levels = ProjectAccessLevelsFor(permission);

        return allowOwner && permission is MemoryAccessPermissions.Read or MemoryAccessPermissions.Write or MemoryAccessPermissions.Review or MemoryAccessPermissions.Admin
            ? [.. levels, "owner"]
            : levels;
    }
}
