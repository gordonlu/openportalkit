using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.AdminHost.Pages.AgentAccess;

public class IndexModel : PageModel
{
    private readonly IAgentOutputArtifactStore _artifactStore;
    private readonly IPublicOutputRevalidationStore _revalidationStore;
    private readonly IAuditLogStore _auditStore;
    private readonly AgentBotPolicyOptions _botPolicyOptions;

    public IndexModel(
        IAgentOutputArtifactStore artifactStore,
        IPublicOutputRevalidationStore revalidationStore,
        IAuditLogStore auditStore,
        IOptions<AgentBotPolicyOptions> botPolicyOptions)
    {
        _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        _revalidationStore = revalidationStore ?? throw new ArgumentNullException(nameof(revalidationStore));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _botPolicyOptions = botPolicyOptions?.Value ?? throw new ArgumentNullException(nameof(botPolicyOptions));
    }

    public IReadOnlyList<AgentMetric> Metrics { get; private set; } = Array.Empty<AgentMetric>();

    public IReadOnlyList<AgentOutput> Outputs { get; private set; } = Array.Empty<AgentOutput>();

    public IReadOnlyList<SnapshotCoverage> SnapshotCoverage { get; private set; } = Array.Empty<SnapshotCoverage>();

    public IReadOnlyList<PolicyItem> Policy { get; private set; } = Array.Empty<PolicyItem>();

    public IReadOnlyList<PipelineItem> Pipeline { get; private set; } = Array.Empty<PipelineItem>();

    public IReadOnlyList<string> Alerts { get; private set; } = Array.Empty<string>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var artifacts = await _artifactStore.ListAsync(cancellationToken);
        var revalidations = await _revalidationStore.ListAsync(cancellationToken);
        var auditCount = await CountAuditLogsAsync(revalidations, cancellationToken);
        var markdownCount = artifacts.Count(artifact => artifact.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        var jsonCount = artifacts.Count(artifact => artifact.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        var contentSources = artifacts
            .Where(artifact => string.Equals(artifact.SourceKind, "ContentItem", StringComparison.OrdinalIgnoreCase))
            .Select(artifact => artifact.SourceId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var readySources = artifacts
            .Where(artifact => string.Equals(artifact.SourceKind, "ContentItem", StringComparison.OrdinalIgnoreCase))
            .GroupBy(artifact => artifact.SourceId, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Any(artifact => artifact.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) &&
                group.Any(artifact => artifact.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)));
        var readinessScore = contentSources == 0 ? 0 : (int)Math.Round(readySources * 100m / contentSources);

        Metrics = new[]
        {
            new AgentMetric("Agent Readiness", readinessScore + "%", $"{readySources} content items fully ready"),
            new AgentMetric("Markdown snapshots", markdownCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "Traceable MD artifacts"),
            new AgentMetric("JSON snapshots", jsonCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "Traceable JSON artifacts"),
            new AgentMetric("OpenAPI readiness", "Ready", "Public read endpoints described")
        };

        Outputs = BuildOutputs(artifacts);
        SnapshotCoverage = BuildSnapshotCoverage(artifacts);
        Policy = BuildPolicy(_botPolicyOptions.ToPolicy());
        Pipeline = BuildPipeline(artifacts, revalidations, auditCount);
        Alerts = BuildAlerts(artifacts, auditCount);
    }

    private static IReadOnlyList<AgentOutput> BuildOutputs(IReadOnlyList<AgentOutputArtifact> artifacts)
    {
        var paths = artifacts.Select(artifact => artifact.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new[]
        {
            new AgentOutput("llms.txt", "/llms.txt", "Concise LLM discovery file", "Generated"),
            new AgentOutput("llms-full.txt", "/llms-full.txt", "Expanded discovery with content excerpts", "Generated"),
            new AgentOutput("agent.json", "/.well-known/agent.json", "Machine-readable agent manifest", "Configured"),
            new AgentOutput("OpenAPI", "/api/openapi.json", "Public read endpoint description", "Ready"),
            new AgentOutput("Markdown artifacts", "/content/{slug}.md", "Persisted Markdown snapshots", paths.Any(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) ? "Ready" : "Missing"),
            new AgentOutput("JSON artifacts", "/api/public/content/{slug}.json", "Persisted JSON snapshots", paths.Any(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ? "Ready" : "Missing")
        };
    }

    private static IReadOnlyList<SnapshotCoverage> BuildSnapshotCoverage(IReadOnlyList<AgentOutputArtifact> artifacts)
    {
        return artifacts
            .Where(artifact => string.Equals(artifact.SourceKind, "ContentItem", StringComparison.OrdinalIgnoreCase))
            .GroupBy(artifact => artifact.SourceId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var markdown = group.FirstOrDefault(artifact => artifact.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                var json = group.FirstOrDefault(artifact => artifact.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                var title = group.Key.Replace("content:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace('-', ' ');

                return new SnapshotCoverage(
                    System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title),
                    "/content/" + group.Key.Replace("content:", string.Empty, StringComparison.OrdinalIgnoreCase),
                    markdown is null ? "Missing" : "Ready",
                    json is null ? "Missing" : "Ready",
                    "Included",
                    json is null ? "Needs JSON artifact" : "Checksum " + json.Checksum[..Math.Min(8, json.Checksum.Length)]);
            })
            .ToArray();
    }

    private static IReadOnlyList<PolicyItem> BuildPolicy(AgentBotPolicy policy)
    {
        return new[]
        {
            new PolicyItem("Search bots", policy.AllowSearchBots ? "Allowed" : "Blocked", "Public pages, sitemap, RSS, and snapshots follow this setting."),
            new PolicyItem("Training bots", policy.AllowTrainingBots ? "Allowed" : "Blocked", "Common training crawlers receive the configured robots.txt policy."),
            new PolicyItem("Allowed user agents", policy.AllowedUserAgents.Count == 0 ? "None" : string.Join(", ", policy.AllowedUserAgents), "Explicit allow-list entries remain crawlable."),
            new PolicyItem("Crawl delay", policy.CrawlDelaySeconds is null ? "Not set" : policy.CrawlDelaySeconds + " seconds", "Automated access pacing for bots that honor crawl-delay.")
        };
    }

    private static IReadOnlyList<PipelineItem> BuildPipeline(
        IReadOnlyList<AgentOutputArtifact> artifacts,
        IReadOnlyList<PublicOutputRevalidationResult> revalidations,
        int auditCount)
    {
        return new[]
        {
            new PipelineItem("Publishing outbox", revalidations.Count > 0 ? "Connected" : "Waiting", "Content publish/update/archive events create public output plans."),
            new PipelineItem("Revalidation plan", revalidations.Count + " runs", "Plans include sitemap, RSS, Markdown, JSON, llms.txt, and llms-full.txt."),
            new PipelineItem("Artifact store", artifacts.Count + " artifacts", "Generated snapshots carry source id, schema version, checksum, and timestamps."),
            new PipelineItem("Audit log", auditCount + " records", "Public-output-changing regeneration writes an audit record.")
        };
    }

    private static IReadOnlyList<string> BuildAlerts(
        IReadOnlyList<AgentOutputArtifact> artifacts,
        int auditCount)
    {
        var alerts = new List<string>();

        if (artifacts.Count == 0)
        {
            alerts.Add("No generated agent output artifacts are available.");
        }

        if (auditCount == 0)
        {
            alerts.Add("No public-output revalidation audit records are available.");
        }

        alerts.Add("Review legal attribution policy before allowing AI training crawlers.");
        return alerts;
    }

    public string StatusClass(string status)
    {
        return status switch
        {
            "Ready" or "Generated" or "Configured" or "Included" => "is-approved",
            "Waiting" => "is-review",
            "Missing" => "is-scheduled",
            _ => "is-draft"
        };
    }

    private async Task<int> CountAuditLogsAsync(
        IReadOnlyList<PublicOutputRevalidationResult> revalidations,
        CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var result in revalidations)
        {
            count += (await _auditStore.FindByTargetAsync(
                "PublicOutput",
                result.SourceIdempotencyKey,
                cancellationToken)).Count;
        }

        return count;
    }
}

public sealed record AgentMetric(string Label, string Value, string Detail);

public sealed record AgentOutput(string Label, string Url, string Description, string Status);

public sealed record SnapshotCoverage(
    string Title,
    string Path,
    string Markdown,
    string Json,
    string Llms,
    string Policy);

public sealed record PolicyItem(string Label, string Value, string Detail);

public sealed record PipelineItem(string Label, string Value, string Detail);
