using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MemorySystem.Infrastructure.Observability;

public static class MemorySystemTelemetry
{
    public const string ServiceName = "MemorySystem";
    public const string ServiceNamespace = "memorysystem";
    public const string ActivitySourceName = "MemorySystem";
    public const string MeterName = "MemorySystem";
    public const string CorrelationHeaderName = "X-Correlation-ID";
    public const string CorrelationItemName = "MemorySystem.CorrelationId";

    public const string ApiRequestSpanName = "MemorySystem.Api.Request";
    public const string ApiIdempotencySpanName = "MemorySystem.Api.Idempotency";
    public const string RetrievalContextPacketSpanName = "MemorySystem.Retrieval.ContextPacket";
    public const string RetrievalQueryFactsSpanName = "MemorySystem.Retrieval.QueryFacts";
    public const string ReviewActionSpanName = "MemorySystem.Review.Action";
    public const string VaultExportRenderSpanName = "MemorySystem.VaultExport.Render";
    public const string WorkerOutboxLeaseSpanName = "MemorySystem.Worker.OutboxLease";
    public const string WorkerOutboxJobSpanName = "MemorySystem.Worker.OutboxJob";
    public const string GovernanceLegalHoldSpanName = "MemorySystem.Governance.LegalHold";
    public const string GovernanceErasureSpanName = "MemorySystem.Governance.Erasure";
    public const string GovernanceRetentionReportSpanName = "MemorySystem.Governance.RetentionReport";

    public const string ServiceRoleAttribute = "memorysystem.service_role";
    public const string CorrelationIdAttribute = "memorysystem.correlation_id";
    public const string ScopeTypeAttribute = "memorysystem.scope_type";
    public const string RoleIdAttribute = "memorysystem.role_id";
    public const string QueryHashAttribute = "memorysystem.query_hash";
    public const string ResultCountAttribute = "memorysystem.result_count";
    public const string FailureStatusAttribute = "memorysystem.failure_status";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ApiRequestCounter = Meter.CreateCounter<long>(
        "memorysystem_api_requests_total");

    public static readonly Histogram<double> ApiRequestDuration = Meter.CreateHistogram<double>(
        "memorysystem_api_request_duration_ms",
        unit: "ms");

    public static readonly Counter<long> OutboxLeaseCounter = Meter.CreateCounter<long>(
        "memorysystem_worker_outbox_leases_total");

    public static readonly Counter<long> OutboxJobCounter = Meter.CreateCounter<long>(
        "memorysystem_worker_outbox_jobs_total");

    public static readonly Histogram<double> OutboxJobDuration = Meter.CreateHistogram<double>(
        "memorysystem_worker_outbox_job_duration_ms",
        unit: "ms");

    public static string? ReadCorrelationId(IDictionary<object, object?> items)
    {
        return items.TryGetValue(CorrelationItemName, out var value)
            ? value as string
            : null;
    }
}
