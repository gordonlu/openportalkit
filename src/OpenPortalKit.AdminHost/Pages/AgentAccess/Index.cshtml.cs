using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OpenPortalKit.AdminHost.Pages.AgentAccess;

public class IndexModel : PageModel
{
    public IReadOnlyList<AgentMetric> Metrics { get; } = new[]
    {
        new AgentMetric("Agent Readiness", "92%", "4 pages fully ready"),
        new AgentMetric("Markdown snapshots", "2/2", "All public content has MD"),
        new AgentMetric("JSON snapshots", "2/2", "All public content has JSON"),
        new AgentMetric("OpenAPI readiness", "Ready", "Public read endpoints described")
    };

    public IReadOnlyList<AgentOutput> Outputs { get; } = new[]
    {
        new AgentOutput("llms.txt", "/llms.txt", "Concise LLM discovery file", "Ready"),
        new AgentOutput("llms-full.txt", "/llms-full.txt", "Expanded discovery with content excerpts", "Ready"),
        new AgentOutput("agent.json", "/.well-known/agent.json", "Machine-readable agent manifest", "Ready"),
        new AgentOutput("OpenAPI", "/api/openapi.json", "Public read endpoint description", "Ready")
    };

    public IReadOnlyList<SnapshotCoverage> SnapshotCoverage { get; } = new[]
    {
        new SnapshotCoverage("OpenPortalKit Launch Notes", "/content/launch-notes", "Ready", "Ready", "Included", "Search indexing allowed"),
        new SnapshotCoverage("Publishing Health Overview", "/content/publishing-health-overview", "Ready", "Ready", "Included", "Training blocked")
    };

    public IReadOnlyList<PolicyItem> Policy { get; } = new[]
    {
        new PolicyItem("Search bots", "Allowed", "Public pages, sitemap, RSS, and snapshots are crawlable."),
        new PolicyItem("Training bots", "Blocked", "Common training crawlers receive Disallow by default."),
        new PolicyItem("Allowed user agents", "OpenPortalKit-Smoke", "Explicit allow-list entries remain crawlable."),
        new PolicyItem("Crawl delay", "2 seconds", "Development default keeps automated access polite.")
    };

    public IReadOnlyList<PipelineItem> Pipeline { get; } = new[]
    {
        new PipelineItem("Publishing outbox", "Connected", "Content publish/update/archive events create public output plans."),
        new PipelineItem("Revalidation plan", "R8 outputs", "Plans include sitemap, RSS, Markdown, JSON, llms.txt, and llms-full.txt."),
        new PipelineItem("Artifact store", "Traceable", "Generated snapshots carry source id, schema version, checksum, and timestamps."),
        new PipelineItem("Audit log", "Recorded", "Public-output-changing regeneration writes an audit record.")
    };

    public IReadOnlyList<string> Alerts { get; } = new[]
    {
        "Connect generated snapshots to persisted content storage before production launch.",
        "Review legal attribution policy before allowing AI training crawlers."
    };

    public void OnGet()
    {
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
