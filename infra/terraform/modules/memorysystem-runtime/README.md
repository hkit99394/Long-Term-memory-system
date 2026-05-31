# memorysystem-runtime

Defines the ECS runtime contract for the multi-role MemorySystem image.

PI-04 adds the backup/export and restore-validation job command contracts,
including expected evidence files and emitted metrics. The module remains
resource-free on purpose; later `PI-*` slices can add the ECS cluster, task
definitions, services, load balancer, task roles, and security groups behind the
variables and outputs defined here.
