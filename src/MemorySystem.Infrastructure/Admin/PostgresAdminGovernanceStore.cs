using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Retention;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed partial class PostgresAdminGovernanceStore : IAdminGovernanceStore
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresAdminGovernanceStore(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }
}
