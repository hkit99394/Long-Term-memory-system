using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class StructuredLoggingTests
{
    private const string TestApiKey = "test-api-key";
    private const string ProposalSecret = "M8_LOGGING_PROPOSAL_SECRET";
    private const string SearchSecret = "M8_LOGGING_SEARCH_SECRET";
    private const string ReviewSecret = "M8_LOGGING_REVIEW_SECRET";
    private const string ReviewNoteSecret = "M8_LOGGING_REVIEW_NOTE_SECRET";

    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Structured_logs_cover_decisions_without_payload_content()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_structured_logging_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var loggerProvider = new CapturingLoggerProvider();
            using var factory = CreateFactory(databaseConnectionString, loggerProvider);
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.OK, await SendProposalAsync(
                client,
                "structured-logging-stored-key",
                CreateProposalBody("logging stored memory", ProposalSecret, confidence: 0.95m)));

            Assert.Equal(HttpStatusCode.OK, await SendSearchAsync(client, SearchSecret));

            var reviewId = await InsertPendingReviewAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.OK, await SendReviewDeleteAsync(client, reviewId));

            var apiLogs = loggerProvider.Entries
                .Where(entry => entry.Category.StartsWith("MemorySystem.Api.", StringComparison.Ordinal))
                .ToArray();

            Assert.Contains(apiLogs, entry => entry.Message.Contains("Memory proposal decision", StringComparison.Ordinal));
            Assert.Contains(apiLogs, entry => entry.Message.Contains("Memory retrieval completed", StringComparison.Ordinal));
            Assert.Contains(apiLogs, entry => entry.Message.Contains("Memory redaction action", StringComparison.Ordinal));

            var renderedLogs = string.Join(
                "\n",
                apiLogs.SelectMany(entry => entry.RenderedValues()));

            Assert.DoesNotContain(ProposalSecret, renderedLogs, StringComparison.Ordinal);
            Assert.DoesNotContain(SearchSecret, renderedLogs, StringComparison.Ordinal);
            Assert.DoesNotContain(ReviewSecret, renderedLogs, StringComparison.Ordinal);
            Assert.DoesNotContain(ReviewNoteSecret, renderedLogs, StringComparison.Ordinal);
            Assert.Contains("QueryLength", renderedLogs, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareDatabaseAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            SourceEventId,
            PrincipalId,
            scopeType: "user",
            scopeId: PrincipalId.ToString());

        var namespacePrefix = $"/user/{PrincipalId}/preferences";

        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            namespacePrefix,
            "write",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            namespacePrefix,
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            namespacePrefix,
            "review",
            principalId: PrincipalId);
    }

    private static string CreateProposalBody(
        string subject,
        string objectValue,
        decimal confidence)
    {
        return JsonSerializer.Serialize(new
        {
            sourceEventId = SourceEventId,
            memoryType = "preference",
            scopeType = "user",
            scopeId = PrincipalId.ToString(),
            @namespace = $"/user/{PrincipalId}/preferences",
            visibility = "private",
            subject,
            predicate = "records",
            @object = objectValue,
            confidence,
            trustLevel = "user_scoped",
            sensitivity = "none"
        });
    }

    private static async Task<HttpStatusCode> SendProposalAsync(
        HttpClient client,
        string idempotencyKey,
        string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/proposals")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> SendSearchAsync(HttpClient client, string query)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/memory/search?q={Uri.EscapeDataString(query)}&limit=5");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> SendReviewDeleteAsync(HttpClient client, Guid reviewId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reviews/{reviewId}/delete")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    sourceEventId = SourceEventId,
                    notes = ReviewNoteSecret
                }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", "structured-logging-delete-key");

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<Guid> InsertPendingReviewAsync(string connectionString)
    {
        var memoryFactId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                user_principal_id,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                trust_level,
                status,
                source_event_id,
                proposed_by_principal_id
            )
            VALUES (
                @memory_fact_id,
                'user',
                @scope_id,
                @namespace,
                @principal_id,
                'preference',
                'private',
                'logging review memory',
                'records',
                @object,
                0.450,
                'user_scoped',
                'tentative',
                @source_event_id,
                @principal_id
            );

            INSERT INTO memory_reviews (
                id,
                memory_fact_id,
                review_status,
                notes,
                source_event_id
            )
            VALUES (
                @review_id,
                @memory_fact_id,
                'pending',
                'Needs review without logging payload.',
                @source_event_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("review_id", reviewId);
        command.Parameters.AddWithValue("scope_id", PrincipalId.ToString());
        command.Parameters.AddWithValue("namespace", $"/user/{PrincipalId}/preferences");
        command.Parameters.AddWithValue("principal_id", PrincipalId);
        command.Parameters.AddWithValue("object", ReviewSecret);
        command.Parameters.AddWithValue("source_event_id", SourceEventId);

        await command.ExecuteNonQueryAsync();

        return reviewId;
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        CapturingLoggerProvider loggerProvider)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = postgresConnectionString,
                        ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = PrincipalId.ToString(),
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Structured logging test"
                    });
                });
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddProvider(loggerProvider);
                });
            });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<CapturedLogEntry> entries = [];

        public IReadOnlyCollection<CapturedLogEntry> Entries => entries;

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(categoryName, entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        string category,
        ConcurrentBag<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToArray()
                : [];

            entries.Add(new CapturedLogEntry(
                category,
                logLevel,
                formatter(state, exception),
                properties));
        }
    }

    private sealed record CapturedLogEntry(
        string Category,
        LogLevel Level,
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> Properties)
    {
        public IEnumerable<string> RenderedValues()
        {
            yield return Category;
            yield return Level.ToString();
            yield return Message;

            foreach (var property in Properties)
            {
                yield return $"{property.Key}={property.Value}";
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
