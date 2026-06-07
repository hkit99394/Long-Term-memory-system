using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using MemorySystem.Api.Http;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Operations;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.MemoryFacts;

public static partial class MemoryFactEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemMemoryFactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/memory/search",
            async (
                HttpContext context,
                IMemoryChunkFullTextSearch search,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await SearchAsync(context, search, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapGet(
            "/api/memory/search/semantic",
            async (
                HttpContext context,
                IMemoryChunkSemanticSearch search,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await SemanticSearchAsync(context, search, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapGet(
            "/api/memory/search/hybrid",
            async (
                HttpContext context,
                IMemoryChunkHybridSearch search,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await HybridSearchAsync(context, search, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapGet(
            "/api/memory/context",
            async (
                HttpContext context,
                IContextPacketBuilder contextPacketBuilder,
                IMemoryContextPacketObservationStore contextPacketObservations,
                IContextProductHealthMetricStore contextProductMetrics,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await BuildContextPacketAsync(context, contextPacketBuilder, contextPacketObservations, contextProductMetrics, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapPost(
            "/api/memory/context/feedback",
            async (
                HttpContext context,
                IMemoryRetrievalFeedbackStore feedbackStore,
                IMemoryContextPacketObservationStore contextPacketObservations,
                IMemoryRetrievalFeedbackSourceAuthorizer feedbackSourceAuthorizer,
                IContextProductHealthMetricStore contextProductMetrics,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await RecordContextFeedbackAsync(
                    context,
                    feedbackStore,
                    contextPacketObservations,
                    feedbackSourceAuthorizer,
                    contextProductMetrics,
                    loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"),
                    cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Mutation);

        endpoints.MapPost(
            "/api/memory/query-facts",
            async (
                HttpContext context,
                IMemoryFactFindingService factFindingService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await QueryFactsAsync(context, factFindingService, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapGet(
            "/api/memory/{id:guid}",
            async (
                Guid id,
                HttpContext context,
                IMemoryFactReadService readService,
                CancellationToken cancellationToken) =>
                await ReadAsync(id, context, readService, cancellationToken))
            .RequireAuthorization();

        return endpoints;
    }
}
