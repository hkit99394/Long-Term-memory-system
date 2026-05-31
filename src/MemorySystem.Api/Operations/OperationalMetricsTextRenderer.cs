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
        AppendContextProductMetrics(builder, summary.ContextProduct);
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

    private static void AppendContextProductMetrics(
        StringBuilder builder,
        OperationalContextProductSummary contextProduct)
    {
        var runtime = contextProduct.Runtime;

        AppendHelp(builder, "memorysystem_context_product_packet_total", "Context product packets built by the API process.");
        AppendType(builder, "memorysystem_context_product_packet_total", "counter");
        AppendSample(builder, "memorysystem_context_product_packet_total", runtime.PacketCount);

        AppendHelp(builder, "memorysystem_context_product_item_total", "Context product items returned by the API process.");
        AppendType(builder, "memorysystem_context_product_item_total", "counter");
        AppendSample(builder, "memorysystem_context_product_item_total", runtime.ItemCount);

        AppendHelp(builder, "memorysystem_context_product_explanation_coverage", "Share of returned context product items with structured explanations.");
        AppendType(builder, "memorysystem_context_product_explanation_coverage", "gauge");
        AppendSample(builder, "memorysystem_context_product_explanation_coverage", decimal.ToDouble(runtime.ExplanationCoverage));

        AppendHelp(builder, "memorysystem_context_product_feedback_action_observed_total", "Context feedback actions observed by the API process.");
        AppendType(builder, "memorysystem_context_product_feedback_action_observed_total", "counter");
        AppendSample(builder, "memorysystem_context_product_feedback_action_observed_total", runtime.FeedbackActionCount);

        AppendContextProductExclusionMetrics(builder, runtime.ExclusionsByReason);
        AppendContextProductFeedbackMetrics(builder, contextProduct.FeedbackActions);
        AppendContextProductReviewOpenMetrics(builder, runtime.ReviewOpens);
        AppendContextProductRankingMetrics(builder, runtime.RankingSignals);
        AppendContextProductBenchmarkMetrics(builder, contextProduct.Benchmark);
    }

    private static void AppendContextProductExclusionMetrics(
        StringBuilder builder,
        IReadOnlyList<OperationalContextProductExclusionSummary> exclusions)
    {
        AppendHelp(builder, "memorysystem_context_product_exclusion_summary_total", "Context product exclusion summaries emitted by safe reason and disclosure mode.");
        AppendType(builder, "memorysystem_context_product_exclusion_summary_total", "counter");
        AppendHelp(builder, "memorysystem_context_product_exclusion_disclosed_item_total", "Disclosed context product exclusion item counts by safe reason.");
        AppendType(builder, "memorysystem_context_product_exclusion_disclosed_item_total", "counter");

        var byKey = exclusions.ToDictionary(
            item => $"{item.Reason}\0{item.CountDisclosure}",
            item => item,
            StringComparer.Ordinal);
        var knownReasons = new[]
        {
            "inactive",
            "not_authorized",
            "scope_mismatch",
            "role_mismatch",
            "below_rank_cutoff",
            "source_unavailable",
            "sensitive"
        };
        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reason in knownReasons)
        {
            var disclosure = reason is "not_authorized" or "sensitive"
                ? "withheld"
                : "disclosed";
            var key = $"{reason}\0{disclosure}";
            emittedKeys.Add(key);
            byKey.TryGetValue(key, out var exclusion);

            AppendSample(
                builder,
                "memorysystem_context_product_exclusion_summary_total",
                exclusion?.SummaryCount ?? 0,
                ("reason", reason),
                ("count_disclosure", disclosure));
            AppendSample(
                builder,
                "memorysystem_context_product_exclusion_disclosed_item_total",
                exclusion?.DisclosedItemCount ?? 0,
                ("reason", reason));
        }

        foreach (var exclusion in exclusions)
        {
            if (emittedKeys.Contains($"{exclusion.Reason}\0{exclusion.CountDisclosure}"))
            {
                continue;
            }

            AppendSample(
                builder,
                "memorysystem_context_product_exclusion_summary_total",
                exclusion.SummaryCount,
                ("reason", exclusion.Reason),
                ("count_disclosure", exclusion.CountDisclosure));
            if (string.Equals(exclusion.CountDisclosure, "disclosed", StringComparison.Ordinal))
            {
                AppendSample(
                    builder,
                    "memorysystem_context_product_exclusion_disclosed_item_total",
                    exclusion.DisclosedItemCount,
                    ("reason", exclusion.Reason));
            }
        }
    }

    private static void AppendContextProductFeedbackMetrics(
        StringBuilder builder,
        OperationalContextProductFeedbackSummary feedback)
    {
        AppendHelp(builder, "memorysystem_context_product_feedback_action_total", "Context product feedback action count in the recent operator window.");
        AppendType(builder, "memorysystem_context_product_feedback_action_total", "gauge");
        AppendHelp(builder, "memorysystem_context_product_feedback_action_share", "Context product feedback action share in the recent operator window.");
        AppendType(builder, "memorysystem_context_product_feedback_action_share", "gauge");
        AppendHelp(builder, "memorysystem_context_product_feedback_action_per_hour", "Context product feedback action rate in the recent operator window.");
        AppendType(builder, "memorysystem_context_product_feedback_action_per_hour", "gauge");

        foreach (var action in feedback.ByAction)
        {
            AppendSample(
                builder,
                "memorysystem_context_product_feedback_action_total",
                action.Count,
                ("feedback_type", action.FeedbackType),
                ("window", "24h"));
            AppendSample(
                builder,
                "memorysystem_context_product_feedback_action_share",
                decimal.ToDouble(action.Share),
                ("feedback_type", action.FeedbackType),
                ("window", "24h"));
            AppendSample(
                builder,
                "memorysystem_context_product_feedback_action_per_hour",
                action.PerHour,
                ("feedback_type", action.FeedbackType),
                ("window", "24h"));
        }
    }

    private static void AppendContextProductReviewOpenMetrics(
        StringBuilder builder,
        IReadOnlyList<OperationalContextProductReviewOpenSummary> reviewOpens)
    {
        AppendHelp(builder, "memorysystem_context_product_review_open_total", "Context feedback observations routed to memory review by feedback type.");
        AppendType(builder, "memorysystem_context_product_review_open_total", "counter");

        var byKey = reviewOpens.ToDictionary(
            item => $"{item.FeedbackType}\0{item.Created}",
            item => item.Count,
            StringComparer.Ordinal);

        foreach (var feedbackType in new[] { "stale", "wrong", "sensitive" })
        {
            foreach (var created in new[] { true, false })
            {
                byKey.TryGetValue($"{feedbackType}\0{created}", out var count);
                AppendSample(
                    builder,
                    "memorysystem_context_product_review_open_total",
                    count,
                    ("feedback_type", feedbackType),
                    ("created", created ? "true" : "false"));
            }
        }
    }

    private static void AppendContextProductRankingMetrics(
        StringBuilder builder,
        IReadOnlyList<OperationalContextProductRankingSignalSummary> rankingSignals)
    {
        AppendHelp(builder, "memorysystem_context_product_ranking_signal_applied_total", "Context product ranking feedback signals applied to returned items.");
        AppendType(builder, "memorysystem_context_product_ranking_signal_applied_total", "counter");

        var bySignal = rankingSignals.ToDictionary(
            item => item.Signal,
            item => item.Count,
            StringComparer.Ordinal);

        foreach (var signal in new[] { "feedback_adjustment_positive", "feedback_adjustment_negative" })
        {
            bySignal.TryGetValue(signal, out var count);
            AppendSample(
                builder,
                "memorysystem_context_product_ranking_signal_applied_total",
                count,
                ("signal", signal));
        }
    }

    private static void AppendContextProductBenchmarkMetrics(
        StringBuilder builder,
        OperationalContextProductBenchmarkSummary benchmark)
    {
        AppendHelp(builder, "memorysystem_context_product_benchmark_observed", "Whether a latest context-product benchmark result was observed.");
        AppendType(builder, "memorysystem_context_product_benchmark_observed", "gauge");
        AppendSample(builder, "memorysystem_context_product_benchmark_observed", benchmark.Observed ? 1 : 0);

        AppendHelp(builder, "memorysystem_context_product_benchmark_delta", "Latest context-product benchmark before-and-after delta by metric.");
        AppendType(builder, "memorysystem_context_product_benchmark_delta", "gauge");
        AppendHelp(builder, "memorysystem_context_product_benchmark_read_error", "Whether the latest context-product benchmark artifact could not be read by reason.");
        AppendType(builder, "memorysystem_context_product_benchmark_read_error", "gauge");

        var byMetric = benchmark.Deltas.ToDictionary(
            delta => delta.Metric,
            delta => delta.Value,
            StringComparer.Ordinal);
        foreach (var metric in new[] { "feedbackAdjustmentDelta", "rankDelta" })
        {
            byMetric.TryGetValue(metric, out var value);
            AppendSample(
                builder,
                "memorysystem_context_product_benchmark_delta",
                value,
                ("metric", metric));
        }

        foreach (var reason in new[] { "invalid_json", "io_error", "missing_metrics" })
        {
            AppendSample(
                builder,
                "memorysystem_context_product_benchmark_read_error",
                string.Equals(benchmark.ReadError, reason, StringComparison.Ordinal) ? 1 : 0,
                ("reason", reason));
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
