namespace OpenPortalKit.Cli.Checks;

public sealed class RepositoryAgentReadinessChecker
{
    private static readonly (string Code, string Target, string[] Files, string[] Tokens)[] Contracts =
    [
        ("OPK-AGT-001", "HTML output",
            ["src/OpenPortalKit.ApiHost/Program.cs"],
            ["/content/{slug}", "/pages/{slug}", "<main>", "<title>", "name=\\\"description\\\"", "rel=\\\"canonical\\\"", "application/ld+json"]),
        ("OPK-AGT-002", "machine-readable snapshots",
            ["src/OpenPortalKit.ApiHost/Program.cs"],
            ["/content/{slug}.md", "/api/public/content/{slug}.json", "/pages/{slug}.md", "/api/public/pages/{slug}.json"]),
        ("OPK-AGT-003", "discovery outputs",
            ["src/OpenPortalKit.ApiHost/Program.cs"],
            ["/robots.txt", "/sitemap.xml", "/rss.xml", "/llms.txt", "/llms-full.txt", "/.well-known/agent.json", "/api/openapi.json"]),
        ("OPK-AGT-004", "dataset outputs",
            ["src/OpenPortalKit.ApiHost/Program.cs"],
            ["/api/public/datasets/{code}/schema", "/api/public/datasets/{code}/records", "/api/public/datasets/{code}/export.csv"]),
        ("OPK-AGT-005", "structured metadata",
            ["src/OpenPortalKit.Modules.Seo/PublicResources/SeoPageMetadataBuilder.cs"],
            ["@context", "https://schema.org", "datePublished", "dateModified"]),
        ("OPK-AGT-006", "public OpenAPI contract",
            ["src/OpenPortalKit.Modules.AgentAccess/AgentOutputs/AgentOpenApiGenerator.cs"],
            ["/api/public/content/{slug}.json", "/api/public/datasets/{code}/schema", "/api/public/datasets/{code}/export.csv", "/sitemap.xml", "/rss.xml"])
    ];

    public CheckReport Run(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        root = Path.GetFullPath(root);
        var results = new List<CheckResult>();

        foreach (var contract in Contracts)
        {
            var missingFiles = contract.Files.Where(file => !File.Exists(Path.Combine(root, file))).ToArray();
            if (missingFiles.Length > 0)
            {
                results.Add(new CheckResult(
                    contract.Code,
                    CheckStatus.Failed,
                    contract.Target,
                    "Required files are missing: " + string.Join(", ", missingFiles)));
                continue;
            }

            var source = string.Join('\n', contract.Files.Select(file => File.ReadAllText(Path.Combine(root, file))));
            var missingTokens = contract.Tokens.Where(token => !source.Contains(token, StringComparison.Ordinal)).ToArray();
            results.Add(missingTokens.Length == 0
                ? new CheckResult(contract.Code, CheckStatus.Passed, contract.Target, "Required repository contracts are present.")
                : new CheckResult(
                    contract.Code,
                    CheckStatus.Failed,
                    contract.Target,
                    "Missing contract markers: " + string.Join(", ", missingTokens)));
        }

        var publicProgram = Path.Combine(root, "src", "OpenPortalKit.ApiHost", "Program.cs");
        var formStatus = File.Exists(publicProgram) && File.ReadAllText(publicProgram).Contains("<form", StringComparison.OrdinalIgnoreCase)
            ? new CheckResult(
                "OPK-AGT-007",
                CheckStatus.Warning,
                "accessible forms",
                "Public forms exist; include them in browser-level accessibility validation.")
            : new CheckResult(
                "OPK-AGT-007",
                CheckStatus.Passed,
                "accessible forms",
                "No public form output is currently exposed; the requirement is not applicable.");
        results.Add(formStatus);

        return new CheckReport("OpenPortalKit repository AgentSEO readiness", results);
    }
}
