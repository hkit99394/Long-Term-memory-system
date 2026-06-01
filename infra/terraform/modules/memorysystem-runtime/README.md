# memorysystem-runtime

Defines the ECS runtime contract for the multi-role MemorySystem image.

PI-04 adds the backup/export and restore-validation job command contracts,
including expected evidence files and emitted metrics. PI-07 adds a release
checklist contract that points at
`docs/production-release-checklists-pi07.md` and names the migration, health,
metrics, benchmark, backup/restore, rollback owner, alert routing, and audit
evidence gates for local, CI, pilot, and production. The module remains
resource-free on purpose. PI-08 records the first isolated platform rehearsal
and exposes the rehearsal command/report in the runtime contract so later ECS
task definitions can preserve the same release evidence path. Later platform
slices can add the ECS cluster, task definitions, services, load balancer, task
roles, and security groups behind the variables and outputs defined here.
