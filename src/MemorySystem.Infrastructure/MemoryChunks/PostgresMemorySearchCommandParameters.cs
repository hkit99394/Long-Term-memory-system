using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryChunks;

internal static class PostgresMemorySearchCommandParameters
{
    private static readonly string[] ReadPermissions =
    [
        "read",
        "write",
        "review",
        "admin"
    ];

    private static readonly string[] ReadProjectAccessLevels =
    [
        "reader",
        "contributor",
        "reviewer",
        "admin"
    ];

    private static readonly string[] ReadOrganizationAccessLevels =
    [
        "reader",
        "contributor",
        "reviewer",
        "admin",
        "owner"
    ];

    private static readonly string[] AdminOrganizationAccessLevels =
    [
        "admin",
        "owner"
    ];

    public static void AddAuthorizationParameters(NpgsqlCommand command)
    {
        command.Parameters.Add("read_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = ReadPermissions;
        command.Parameters.Add("read_project_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = ReadProjectAccessLevels;
        command.Parameters.Add("read_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = ReadOrganizationAccessLevels;
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = AdminOrganizationAccessLevels;
    }
}
