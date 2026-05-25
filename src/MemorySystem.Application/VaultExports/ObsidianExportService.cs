using System.Globalization;
using System.Text;
using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.VaultExports;

public sealed class ObsidianExportService(
    IObsidianExportCandidateStore candidateStore,
    IMemoryAccessAuthorizer accessAuthorizer) : IObsidianExportService
{
    public const int MaxLimit = 100;
    private const int CandidateReadLimit = 500;

    public async Task<ObsidianExportBundle> ExportAsync(
        ObsidianExportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, $"Obsidian export limit must be between 1 and {MaxLimit}.");
        }

        var (scopeType, scopeId) = NormalizeScope(query);
        var candidates = await candidateStore.ListCandidatesAsync(
            new ObsidianExportCandidateQuery(scopeType, scopeId, CandidateReadLimit),
            cancellationToken);
        var documents = new List<ObsidianExportDocument>(query.Limit);

        foreach (var candidate in candidates)
        {
            var memoryFact = candidate.MemoryFact;
            var access = await accessAuthorizer.AuthorizeAsync(
                new MemoryAccessRequest(
                    query.PrincipalId,
                    MemoryAccessPermissions.Read,
                    memoryFact.ToScopeResolution(),
                    memoryFact.Namespace),
                cancellationToken);

            if (!access.Allowed)
            {
                continue;
            }

            documents.Add(RenderDocument(candidate));

            if (documents.Count == query.Limit)
            {
                break;
            }
        }

        await candidateStore.RecordExportedAsync(documents, cancellationToken);

        var staleCandidates = await candidateStore.ListStaleCandidatesAsync(
            new ObsidianExportCandidateQuery(scopeType, scopeId, CandidateReadLimit),
            cancellationToken);
        var staleDocuments = new List<ObsidianStaleExportDocument>(query.Limit);

        foreach (var candidate in staleCandidates)
        {
            var memoryFact = candidate.MemoryFact;
            var access = await accessAuthorizer.AuthorizeAsync(
                new MemoryAccessRequest(
                    query.PrincipalId,
                    MemoryAccessPermissions.Read,
                    memoryFact.ToScopeResolution(),
                    memoryFact.Namespace),
                cancellationToken);

            if (!access.Allowed)
            {
                continue;
            }

            staleDocuments.Add(RenderStaleDocument(candidate));

            if (staleDocuments.Count == query.Limit)
            {
                break;
            }
        }

        await candidateStore.RecordStaleAsync(staleDocuments, cancellationToken);

        return new ObsidianExportBundle(DateTimeOffset.UtcNow, documents, staleDocuments);
    }

    public async Task<ObsidianArchiveExportBundle> ExportArchiveAsync(
        ObsidianExportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, $"Obsidian archive export limit must be between 1 and {MaxLimit}.");
        }

        var (scopeType, scopeId) = NormalizeScope(query);
        var candidates = await candidateStore.ListArchiveCandidatesAsync(
            new ObsidianExportCandidateQuery(scopeType, scopeId, CandidateReadLimit),
            cancellationToken);
        var documents = new List<ObsidianArchiveExportDocument>(query.Limit);

        foreach (var candidate in candidates)
        {
            var memoryFact = candidate.MemoryFact;
            var access = await accessAuthorizer.AuthorizeAsync(
                new MemoryAccessRequest(
                    query.PrincipalId,
                    MemoryAccessPermissions.Read,
                    memoryFact.ToScopeResolution(),
                    memoryFact.Namespace),
                cancellationToken);

            if (!access.Allowed)
            {
                continue;
            }

            documents.Add(RenderArchiveDocument(candidate));

            if (documents.Count == query.Limit)
            {
                break;
            }
        }

        await candidateStore.RecordArchiveAsync(documents, cancellationToken);

        return new ObsidianArchiveExportBundle(DateTimeOffset.UtcNow, documents);
    }

    private static (string? ScopeType, string? ScopeId) NormalizeScope(ObsidianExportQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.ScopeType) && string.IsNullOrWhiteSpace(query.ScopeId))
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(query.ScopeType) || string.IsNullOrWhiteSpace(query.ScopeId))
        {
            throw new ArgumentException("Obsidian export scope type and id must be provided together.", nameof(query));
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            query.ScopeType,
            query.ScopeId,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out var error))
        {
            throw new ArgumentException(error, nameof(query));
        }

        return (normalizedScopeType, normalizedScopeId);
    }

    private static ObsidianExportDocument RenderDocument(ObsidianExportCandidate candidate)
    {
        var memoryFact = candidate.MemoryFact;
        var title = memoryFact.Subject.Trim();
        var sourceLink = BuildSourceLink(memoryFact.SourceEventId);
        var path = BuildVaultPath(memoryFact);
        var content = BuildMarkdown(candidate, title, sourceLink);

        return new ObsidianExportDocument(
            path,
            title,
            memoryFact.MemoryType,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Namespace,
            memoryFact.Id,
            memoryFact.SourceEventId,
            sourceLink,
            content);
    }

    private static string BuildMarkdown(
        ObsidianExportCandidate candidate,
        string title,
        string sourceLink)
    {
        var memoryFact = candidate.MemoryFact;
        var confidence = memoryFact.Confidence.ToString("0.000", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();

        builder.AppendLine("---");
        AppendYaml(builder, "memory_fact_id", memoryFact.Id.ToString());
        AppendYaml(builder, "source_event_id", memoryFact.SourceEventId.ToString());
        AppendYaml(builder, "source_link", sourceLink);
        AppendYaml(builder, "memory_type", memoryFact.MemoryType);
        AppendYaml(builder, "scope_type", memoryFact.ScopeType);
        AppendYaml(builder, "scope_id", memoryFact.ScopeId);
        AppendYaml(builder, "namespace", memoryFact.Namespace);
        AppendYaml(builder, "trust_level", memoryFact.TrustLevel);
        builder.AppendLine($"confidence: {confidence}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"- Memory fact: `{memoryFact.Id}`");
        builder.AppendLine($"- Source event: `{memoryFact.SourceEventId}`");
        builder.AppendLine($"- Source link: `{sourceLink}`");
        builder.AppendLine($"- Namespace: `{memoryFact.Namespace}`");
        builder.AppendLine($"- Predicate: `{memoryFact.Predicate}`");
        builder.AppendLine($"- Updated: `{candidate.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)}`");
        builder.AppendLine();
        builder.AppendLine(memoryFact.Object.Trim());

        return builder.ToString();
    }

    private static ObsidianStaleExportDocument RenderStaleDocument(ObsidianStaleExportCandidate candidate)
    {
        var memoryFact = candidate.MemoryFact;
        var sourceLink = BuildSourceLink(memoryFact.SourceEventId);

        return new ObsidianStaleExportDocument(
            candidate.ExportPath,
            memoryFact.Id,
            memoryFact.SourceEventId,
            sourceLink,
            memoryFact.MemoryType,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Namespace,
            memoryFact.Status,
            candidate.Reason,
            BuildStaleMarkdown(candidate, sourceLink));
    }

    private static ObsidianArchiveExportDocument RenderArchiveDocument(ObsidianArchiveExportCandidate candidate)
    {
        var memoryFact = candidate.MemoryFact;
        var title = memoryFact.Subject.Trim();
        var sourceLink = BuildSourceLink(memoryFact.SourceEventId);
        var path = BuildArchivePath(memoryFact);

        return new ObsidianArchiveExportDocument(
            path,
            title,
            memoryFact.Id,
            memoryFact.SourceEventId,
            sourceLink,
            memoryFact.MemoryType,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Namespace,
            memoryFact.Status,
            BuildArchiveMarkdown(candidate, title, sourceLink));
    }

    private static string BuildStaleMarkdown(ObsidianStaleExportCandidate candidate, string sourceLink)
    {
        var memoryFact = candidate.MemoryFact;
        var builder = new StringBuilder();

        builder.AppendLine("---");
        AppendYaml(builder, "memory_fact_id", memoryFact.Id.ToString());
        AppendYaml(builder, "source_event_id", memoryFact.SourceEventId.ToString());
        AppendYaml(builder, "source_link", sourceLink);
        AppendYaml(builder, "memory_type", memoryFact.MemoryType);
        AppendYaml(builder, "scope_type", memoryFact.ScopeType);
        AppendYaml(builder, "scope_id", memoryFact.ScopeId);
        AppendYaml(builder, "namespace", memoryFact.Namespace);
        AppendYaml(builder, "status", memoryFact.Status);
        AppendYaml(builder, "stale_reason", candidate.Reason);
        builder.AppendLine("stale: true");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Stale Memory Export");
        builder.AppendLine();
        builder.AppendLine($"- Memory fact: `{memoryFact.Id}`");
        builder.AppendLine($"- Source event: `{memoryFact.SourceEventId}`");
        builder.AppendLine($"- Source link: `{sourceLink}`");
        builder.AppendLine($"- Status: `{memoryFact.Status}`");
        builder.AppendLine($"- Reason: `{candidate.Reason}`");
        builder.AppendLine($"- Previous export path: `{candidate.ExportPath}`");
        builder.AppendLine($"- Previous export recorded: `{candidate.ExportedAt.ToString("O", CultureInfo.InvariantCulture)}`");
        builder.AppendLine();
        builder.AppendLine("This vault note is stale. The authoritative memory record is no longer active and its previous exported content must not be used as current memory.");

        return builder.ToString();
    }

    private static string BuildArchiveMarkdown(
        ObsidianArchiveExportCandidate candidate,
        string title,
        string sourceLink)
    {
        var memoryFact = candidate.MemoryFact;
        var confidence = memoryFact.Confidence.ToString("0.000", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();

        builder.AppendLine("---");
        AppendYaml(builder, "memory_fact_id", memoryFact.Id.ToString());
        AppendYaml(builder, "source_event_id", memoryFact.SourceEventId.ToString());
        AppendYaml(builder, "source_link", sourceLink);
        AppendYaml(builder, "memory_type", memoryFact.MemoryType);
        AppendYaml(builder, "scope_type", memoryFact.ScopeType);
        AppendYaml(builder, "scope_id", memoryFact.ScopeId);
        AppendYaml(builder, "namespace", memoryFact.Namespace);
        AppendYaml(builder, "status", memoryFact.Status);
        AppendYaml(builder, "trust_level", memoryFact.TrustLevel);
        builder.AppendLine($"confidence: {confidence}");
        builder.AppendLine("archive: true");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"- Memory fact: `{memoryFact.Id}`");
        builder.AppendLine($"- Source event: `{memoryFact.SourceEventId}`");
        builder.AppendLine($"- Source link: `{sourceLink}`");
        builder.AppendLine($"- Status: `{memoryFact.Status}`");
        builder.AppendLine($"- Namespace: `{memoryFact.Namespace}`");
        builder.AppendLine($"- Predicate: `{memoryFact.Predicate}`");
        builder.AppendLine($"- Archived from update: `{candidate.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)}`");
        builder.AppendLine();
        builder.AppendLine(memoryFact.Object.Trim());

        return builder.ToString();
    }

    private static string BuildVaultPath(MemoryFactRecord memoryFact)
    {
        var section = memoryFact.MemoryType switch
        {
            "decision" => "Decisions",
            "summary" => "Summaries",
            _ => "Memory"
        };
        var slug = Slug(memoryFact.Subject);
        var idSuffix = memoryFact.Id.ToString("N")[..8];
        var fileName = $"{slug}-{idSuffix}.md";

        return memoryFact.ScopeType switch
        {
            "global" => $"00 Global/{section}/{fileName}",
            "user" => $"10 Users/{memoryFact.ScopeId}/{section}/{fileName}",
            "project" => $"20 Projects/{memoryFact.ScopeId}/{section}/{fileName}",
            "role" => $"30 Roles/{memoryFact.ScopeId}/{section}/{fileName}",
            "org" => $"00 Global/Organizations/{memoryFact.ScopeId}/{section}/{fileName}",
            "agent" => $"90 Archive/Agents/{memoryFact.ScopeId}/{section}/{fileName}",
            "session" => $"90 Archive/Sessions/{memoryFact.ScopeId}/{section}/{fileName}",
            _ => $"90 Archive/{memoryFact.ScopeType}/{memoryFact.ScopeId}/{section}/{fileName}"
        };
    }

    private static string BuildArchivePath(MemoryFactRecord memoryFact)
    {
        var section = memoryFact.MemoryType switch
        {
            "decision" => "Decisions",
            "summary" => "Summaries",
            "preference" => "Preferences",
            _ => "Memory"
        };
        var slug = Slug(memoryFact.Subject);
        var idSuffix = memoryFact.Id.ToString("N")[..8];
        var fileName = $"{slug}-{idSuffix}.md";

        return memoryFact.ScopeType switch
        {
            "global" => $"90 Archive/00 Global/{memoryFact.Status}/{section}/{fileName}",
            "user" => $"90 Archive/10 Users/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}",
            "project" => $"90 Archive/20 Projects/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}",
            "role" => $"90 Archive/30 Roles/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}",
            "org" => $"90 Archive/Organizations/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}",
            "agent" => $"90 Archive/Agents/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}",
            "session" => $"90 Archive/Sessions/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}",
            _ => $"90 Archive/{memoryFact.ScopeType}/{memoryFact.ScopeId}/{memoryFact.Status}/{section}/{fileName}"
        };
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        var previousWasHyphen = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasHyphen = false;
            }
            else if (!previousWasHyphen)
            {
                builder.Append('-');
                previousWasHyphen = true;
            }

            if (builder.Length >= 72)
            {
                break;
            }
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length == 0 ? "memory" : slug;
    }

    private static void AppendYaml(StringBuilder builder, string key, string value)
    {
        builder.Append(key);
        builder.Append(": \"");
        builder.Append(value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal));
        builder.AppendLine("\"");
    }

    private static string BuildSourceLink(Guid sourceEventId)
    {
        return $"/api/events/{sourceEventId}";
    }
}
