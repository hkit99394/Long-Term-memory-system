# memorysystem-postgres

Defines the managed PostgreSQL resources for the MemorySystem platform
baseline.

PI-03 provisions Amazon RDS PostgreSQL with private subnet placement, a database
security group, explicit client ingress controls, storage encryption, RDS-managed
master credential material, backup/PITR settings, final snapshot behavior, and a
pgvector validation contract.

The module does not run SQL. Extension creation stays in
`migrations/001_initial_memory_schema.sql`, and the module exposes the validation
query that platform smoke jobs can run after migration:

```sql
SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';
```
