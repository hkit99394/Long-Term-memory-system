using System.Globalization;
using System.Text;
using MemorySystem.Application.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MemorySystem.Api.Operations;

public static class OperationalMetricsTextRenderer
{
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

    public static string Render(
        OperationalSummary summary,
        HealthReport readiness,
        IReadOnlyList<ApiRequestMetricSnapshot> requestMetrics)
    {
        var builder = new StringBuilder();

        AppendRequestMetrics(builder, requestMetrics);
        AppendReadinessMetrics(builder, readiness);
        AppendOutboxMetrics(builder, summary.Outbox);
        AppendWorkerMetrics(builder, summary.Worker);
        AppendRetrievalFeedbackMetrics(builder, summary.RetrievalFeedback);
        AppendEmbeddingMetrics(builder, summary.EmbeddingFailures, readiness);
        AppendGovernanceMetrics(builder, summary);

        return builder.ToString();
    }

    private static void AppendRequestMetrics(
        StringBuilder builder,
        IReadOnlyList<ApiRequestMetricSnapshot> requestMetrics)
    {
        AppendHelp(builder, "memorysystem_api_requests_total", "Total API requests observed by the API process.");
        AppendType(builder, "memorysystem_api_requests_total", "counter");
        foreach (var metric in requestMetrics)
        {
            AppendSample(
                builder,
                "memorysystem_api_requests_total",
                metric.Count,
                ("method", metric.Method),
                ("route", metric.Route),
                ("status_code", metric.StatusCode.ToString(CultureInfo.InvariantCulture)));
        }

        AppendHelp(builder, "memorysystem_api_request_failures_total", "Total API requests that returned a 5xx status.");
        AppendType(builder, "memorysystem_api_request_failures_total", "counter");
        foreach (var metric in requestMetrics)
        {
            AppendSample(
                builder,
                "memorysystem_api_request_failures_total",
                metric.FailureCount,
                ("method", metric.Method),
                ("route", metric.Route),
                ("status_code", metric.StatusCode.ToString(CultureInfo.InvariantCulture)));
        }

        AppendHelp(builder, "memorysystem_api_request_duration_seconds_count", "Total API requests included in the duration sum.");
        AppendType(builder, "memorysystem_api_request_duration_seconds_count", "counter");
        foreach (var metric in requestMetrics)
        {
            AppendSample(
                builder,
                "memorysystem_api_request_duration_seconds_count",
                metric.Count,
                ("method", metric.Method),
                ("route", metric.Route),
                ("status_code", metric.StatusCode.ToString(CultureInfo.InvariantCulture)));
        }

        AppendHelp(builder, "memorysystem_api_request_duration_seconds_sum", "Total API request duration in seconds.");
        AppendType(builder, "memorysystem_api_request_duration_seconds_sum", "counter");
        foreach (var metric in requestMetrics)
        {
            AppendSample(
                builder,
                "memorysystem_api_request_duration_seconds_sum",
                metric.DurationSecondsSum,
                ("method", metric.Method),
                ("route", metric.Route),
                ("status_code", metric.StatusCode.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendReadinessMetrics(StringBuilder builder, HealthReport readiness)
    {
        AppendHelp(builder, "memorysystem_health_ready", "Readiness state of the API process and required dependencies.");
        AppendType(builder, "memorysystem_health_ready", "gauge");
        AppendSample(builder, "memorysystem_health_ready", readiness.Status == HealthStatus.Healthy ? 1 : 0);

        AppendHelp(builder, "memorysystem_health_check_status", "Readiness health-check status as a labeled gauge.");
        AppendType(builder, "memorysystem_health_check_status", "gauge");
        foreach (var entry in readiness.Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            AppendSample(
                builder,
                "memorysystem_health_check_status",
                1,
                ("check", entry.Key),
                ("status", ToMetricLabel(entry.Value.Status)));
        }
    }

    private static void AppendOutboxMetrics(StringBuilder builder, OperationalOutboxSummary outbox)
    {
        AppendHelp(builder, "memorysystem_outbox_ready_pending", "Outbox jobs ready for processing.");
        AppendType(builder, "memorysystem_outbox_ready_pending", "gauge");
        AppendSample(builder, "memorysystem_outbox_ready_pending", outbox.ReadyPending);

        AppendHelp(builder, "memorysystem_outbox_delayed_pending", "Outbox jobs pending for future processing.");
        AppendType(builder, "memorysystem_outbox_delayed_pending", "gauge");
        AppendSample(builder, "memorysystem_outbox_delayed_pending", outbox.DelayedPending);

        AppendHelp(builder, "memorysystem_outbox_processing", "Outbox jobs currently leased for processing.");
        AppendType(builder, "memorysystem_outbox_processing", "gauge");
        AppendSample(builder, "memorysystem_outbox_processing", outbox.Processing);

        AppendHelp(builder, "memorysystem_outbox_dead_letter", "Outbox jobs in dead-letter state.");
        AppendType(builder, "memorysystem_outbox_dead_letter", "gauge");
        AppendSample(builder, "memorysystem_outbox_dead_letter", outbox.DeadLetter);

        AppendHelp(builder, "memorysystem_outbox_failed", "Outbox jobs in failed state.");
        AppendType(builder, "memorysystem_outbox_failed", "gauge");
        AppendSample(builder, "memorysystem_outbox_failed", outbox.Failed);

        AppendHelp(builder, "memorysystem_outbox_retrying_failed", "Pending outbox jobs with a previous failure.");
        AppendType(builder, "memorysystem_outbox_retrying_failed", "gauge");
        AppendSample(builder, "memorysystem_outbox_retrying_failed", outbox.RetryingFailed);

        AppendHelp(builder, "memorysystem_outbox_expired_processing", "Outbox jobs with expired processing leases.");
        AppendType(builder, "memorysystem_outbox_expired_processing", "gauge");
        AppendSample(builder, "memorysystem_outbox_expired_processing", outbox.ExpiredProcessing);

        AppendHelp(builder, "memorysystem_outbox_oldest_ready_pending_age_seconds", "Age of the oldest ready pending outbox job.");
        AppendType(builder, "memorysystem_outbox_oldest_ready_pending_age_seconds", "gauge");
        AppendSample(builder, "memorysystem_outbox_oldest_ready_pending_age_seconds", outbox.OldestReadyPendingSeconds);
    }

    private static void AppendWorkerMetrics(StringBuilder builder, OperationalWorkerSummary worker)
    {
        AppendHelp(builder, "memorysystem_worker_heartbeat_observed", "Whether a worker heartbeat has been observed.");
        AppendType(builder, "memorysystem_worker_heartbeat_observed", "gauge");
        AppendSample(
            builder,
            "memorysystem_worker_heartbeat_observed",
            worker.Observed ? 1 : 0,
            ("worker_type", worker.WorkerType));

        AppendHelp(builder, "memorysystem_worker_heartbeat_stale", "Whether the latest worker heartbeat is stale.");
        AppendType(builder, "memorysystem_worker_heartbeat_stale", "gauge");
        AppendSample(
            builder,
            "memorysystem_worker_heartbeat_stale",
            worker.Stale ? 1 : 0,
            ("worker_type", worker.WorkerType));

        AppendHelp(builder, "memorysystem_worker_last_seen_age_seconds", "Age of the latest worker heartbeat.");
        AppendType(builder, "memorysystem_worker_last_seen_age_seconds", "gauge");
        AppendSample(
            builder,
            "memorysystem_worker_last_seen_age_seconds",
            worker.LastSeenAgeSeconds ?? 0,
            ("worker_type", worker.WorkerType));

        AppendHelp(builder, "memorysystem_worker_status", "Latest worker status.");
        AppendType(builder, "memorysystem_worker_status", "gauge");
        AppendSample(
            builder,
            "memorysystem_worker_status",
            1,
            ("worker_type", worker.WorkerType),
            ("status", worker.Status));
    }

    private static void AppendRetrievalFeedbackMetrics(
        StringBuilder builder,
        OperationalRetrievalFeedbackSummary retrievalFeedback)
    {
        AppendHelp(builder, "memorysystem_retrieval_feedback_total", "Retrieval feedback count in the recent operator window.");
        AppendType(builder, "memorysystem_retrieval_feedback_total", "gauge");
        AppendSample(builder, "memorysystem_retrieval_feedback_total", retrievalFeedback.Total, ("window", "24h"));

        AppendHelp(builder, "memorysystem_retrieval_feedback_type_total", "Retrieval feedback count by type in the recent operator window.");
        AppendType(builder, "memorysystem_retrieval_feedback_type_total", "gauge");
        AppendHelp(builder, "memorysystem_retrieval_feedback_type_share", "Retrieval feedback share by type in the recent operator window.");
        AppendType(builder, "memorysystem_retrieval_feedback_type_share", "gauge");
        AppendHelp(builder, "memorysystem_retrieval_feedback_type_per_hour", "Retrieval feedback rate by type in the recent operator window.");
        AppendType(builder, "memorysystem_retrieval_feedback_type_per_hour", "gauge");

        foreach (var feedbackType in retrievalFeedback.ByType)
        {
            AppendSample(
                builder,
                "memorysystem_retrieval_feedback_type_total",
                feedbackType.Count,
                ("feedback_type", feedbackType.FeedbackType),
                ("window", "24h"));
            AppendSample(
                builder,
                "memorysystem_retrieval_feedback_type_share",
                decimal.ToDouble(feedbackType.Share),
                ("feedback_type", feedbackType.FeedbackType),
                ("window", "24h"));
            AppendSample(
                builder,
                "memorysystem_retrieval_feedback_type_per_hour",
                feedbackType.PerHour,
                ("feedback_type", feedbackType.FeedbackType),
                ("window", "24h"));
        }
    }

    private static void AppendEmbeddingMetrics(
        StringBuilder builder,
        OperationalEmbeddingFailureSummary embeddingFailures,
        HealthReport readiness)
    {
        AppendHelp(builder, "memorysystem_embedding_provider_ready", "Whether embedding provider readiness is healthy.");
        AppendType(builder, "memorysystem_embedding_provider_ready", "gauge");
        var providerReady = readiness.Entries.TryGetValue("embedding_provider", out var entry)
            && entry.Status == HealthStatus.Healthy;
        AppendSample(builder, "memorysystem_embedding_provider_ready", providerReady ? 1 : 0);

        AppendHelp(builder, "memorysystem_embedding_outbox_retrying_failed", "Memory-index embedding jobs pending after a failure.");
        AppendType(builder, "memorysystem_embedding_outbox_retrying_failed", "gauge");
        AppendSample(builder, "memorysystem_embedding_outbox_retrying_failed", embeddingFailures.RetryingFailed);

        AppendHelp(builder, "memorysystem_embedding_outbox_dead_letter", "Memory-index embedding jobs in dead-letter state.");
        AppendType(builder, "memorysystem_embedding_outbox_dead_letter", "gauge");
        AppendSample(builder, "memorysystem_embedding_outbox_dead_letter", embeddingFailures.DeadLetter);

        AppendHelp(builder, "memorysystem_embedding_outbox_failed", "Memory-index embedding jobs in failed state.");
        AppendType(builder, "memorysystem_embedding_outbox_failed", "gauge");
        AppendSample(builder, "memorysystem_embedding_outbox_failed", embeddingFailures.Failed);

        AppendHelp(builder, "memorysystem_embedding_outbox_expired_processing", "Memory-index embedding jobs with expired processing leases.");
        AppendType(builder, "memorysystem_embedding_outbox_expired_processing", "gauge");
        AppendSample(builder, "memorysystem_embedding_outbox_expired_processing", embeddingFailures.ExpiredProcessing);
    }

    private static void AppendGovernanceMetrics(StringBuilder builder, OperationalSummary summary)
    {
        AppendHelp(builder, "memorysystem_reviews_pending", "Pending human memory reviews.");
        AppendType(builder, "memorysystem_reviews_pending", "gauge");
        AppendSample(builder, "memorysystem_reviews_pending", summary.Reviews.Pending);

        AppendHelp(builder, "memorysystem_vault_exports_stale", "Stale Obsidian vault exports.");
        AppendType(builder, "memorysystem_vault_exports_stale", "gauge");
        AppendSample(builder, "memorysystem_vault_exports_stale", summary.VaultExports.Stale);
    }

    private static void AppendHelp(StringBuilder builder, string metricName, string help)
    {
        builder
            .Append("# HELP ")
            .Append(metricName)
            .Append(' ')
            .AppendLine(help);
    }

    private static void AppendType(StringBuilder builder, string metricName, string type)
    {
        builder
            .Append("# TYPE ")
            .Append(metricName)
            .Append(' ')
            .AppendLine(type);
    }

    private static void AppendSample(
        StringBuilder builder,
        string metricName,
        long value,
        params (string Label, string Value)[] labels)
    {
        AppendSample(builder, metricName, value.ToString(CultureInfo.InvariantCulture), labels);
    }

    private static void AppendSample(
        StringBuilder builder,
        string metricName,
        int value,
        params (string Label, string Value)[] labels)
    {
        AppendSample(builder, metricName, value.ToString(CultureInfo.InvariantCulture), labels);
    }

    private static void AppendSample(
        StringBuilder builder,
        string metricName,
        double value,
        params (string Label, string Value)[] labels)
    {
        AppendSample(builder, metricName, value.ToString("G17", CultureInfo.InvariantCulture), labels);
    }

    private static void AppendSample(
        StringBuilder builder,
        string metricName,
        string value,
        params (string Label, string Value)[] labels)
    {
        builder.Append(metricName);

        if (labels.Length > 0)
        {
            builder.Append('{');
            for (var i = 0; i < labels.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder
                    .Append(labels[i].Label)
                    .Append("=\"")
                    .Append(EscapeLabelValue(labels[i].Value))
                    .Append('"');
            }

            builder.Append('}');
        }

        builder
            .Append(' ')
            .AppendLine(value);
    }

    private static string EscapeLabelValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string ToMetricLabel(HealthStatus status)
    {
        return status.ToString().ToLowerInvariant();
    }
}
