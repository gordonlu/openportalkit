using System.Net;
using System.Text;
using OpenPortalKit.Cli;
using OpenPortalKit.Cli.Checks;

var tests = new (string Name, Func<Task> Run)[]
{
    ("CLI exposes stable help and usage errors", CliExposesStableHelpAndUsageErrors),
    ("repository boundary check passes the product tree", RepositoryBoundaryCheckPasses),
    ("boundary check detects industry leakage", BoundaryCheckDetectsIndustryLeakage),
    ("repository AgentSEO contract is complete", RepositoryAgentContractIsComplete),
    ("live AgentSEO check validates representations", LiveAgentCheckValidatesRepresentations),
    ("live AgentSEO check rejects cross-origin discovery", LiveAgentCheckRejectsCrossOriginDiscovery),
    ("CLI validates industry packs through the shared contract", CliValidatesIndustryPack),
    ("CLI scaffolds a valid industry pack without overwriting", CliScaffoldsIndustryPack),
    ("CLI produces a traceable legacy dry-run report", CliProducesLegacyDryRunReport),
    ("CLI blocks unsafe legacy rows", CliBlocksUnsafeLegacyRows)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static async Task CliExposesStableHelpAndUsageErrors()
{
    var output = new StringWriter();
    var error = new StringWriter();
    var application = new CliApplication(output, error);

    Assert(await application.RunAsync(["--help"]) == 0, "Help should succeed.");
    Assert(output.ToString().Contains("check-agent-readiness", StringComparison.Ordinal), "Help omitted a command.");
    Assert(await application.RunAsync(["unknown"]) == 2, "Unknown commands should return usage exit code 2.");
}

static Task RepositoryBoundaryCheckPasses()
{
    var report = new BoundaryChecker().Run(FindRepositoryRoot());
    Assert(report.IsSuccessful, string.Join("; ", report.Results.Where(result => result.Status == CheckStatus.Failed).Select(result => result.Message)));
    Assert(report.Results.Count >= 4, "Expected all boundary rule families.");
    return Task.CompletedTask;
}

static Task BoundaryCheckDetectsIndustryLeakage()
{
    var root = Path.Combine(Path.GetTempPath(), "opk-cli-tests", Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "OpenPortalKit.Kernel"));
        Directory.CreateDirectory(Path.Combine(root, "src", "OpenPortalKit.ApiHost"));
        Directory.CreateDirectory(Path.Combine(root, "db", "postgresql", "migrations"));
        Directory.CreateDirectory(Path.Combine(root, "tests"));
        File.WriteAllText(Path.Combine(root, "src", "OpenPortalKit.Kernel", "Leak.cs"), "public sealed class Finance { }");
        File.WriteAllText(Path.Combine(root, "src", "OpenPortalKit.ApiHost", "Program.cs"), string.Empty);

        var report = new BoundaryChecker().Run(root);
        Assert(report.Results.Single(result => result.Code == "OPK-BND-001").Status == CheckStatus.Failed,
            "Industry leakage was not detected.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    return Task.CompletedTask;
}

static Task RepositoryAgentContractIsComplete()
{
    var report = new RepositoryAgentReadinessChecker().Run(FindRepositoryRoot());
    Assert(report.IsSuccessful, string.Join("; ", report.Results.Where(result => result.Status == CheckStatus.Failed).Select(result => result.Message)));
    return Task.CompletedTask;
}

static async Task LiveAgentCheckValidatesRepresentations()
{
    using var httpClient = new HttpClient(new AgentReadyHandler());
    var report = await new HttpAgentReadinessChecker(httpClient)
        .RunAsync(new Uri("https://portal.test"));

    Assert(report.IsSuccessful, string.Join("; ", report.Results.Where(result => result.Status == CheckStatus.Failed).Select(result => result.Message)));
    Assert(report.WarningCount == 0, "The complete live fixture should not produce warnings.");
}

static async Task LiveAgentCheckRejectsCrossOriginDiscovery()
{
    using var httpClient = new HttpClient(new AgentReadyHandler(crossOriginContent: true));
    var report = await new HttpAgentReadinessChecker(httpClient)
        .RunAsync(new Uri("https://portal.test"));

    var contentResult = report.Results.Single(result => result.Code == "OPK-AGT-107");
    Assert(contentResult.Status == CheckStatus.Failed, "Cross-origin content discovery should fail closed.");
    Assert(contentResult.Message.Contains("cross-origin", StringComparison.Ordinal), "Failure should explain the origin violation.");
}

static async Task CliValidatesIndustryPack()
{
    var output = new StringWriter();
    var error = new StringWriter();
    var application = new CliApplication(output, error);
    var packPath = Path.Combine(FindRepositoryRoot(), "industry-packs", "Technology");

    var exitCode = await application.RunAsync(["industry-pack", "validate", "--path", packPath, "--format", "json"]);

    Assert(exitCode == 0, error.ToString() + output);
    Assert(output.ToString().Contains("\"Status\": \"passed\"", StringComparison.Ordinal),
        "Pack validation did not produce a passing JSON report.");

    output.GetStringBuilder().Clear();
    var portfolioExitCode = await application.RunAsync([
        "industry-pack", "validate", "--path", Path.Combine(FindRepositoryRoot(), "industry-packs")
    ]);
    Assert(portfolioExitCode == 0, error.ToString() + output);
    Assert(output.ToString().Contains("Finance/pack.json", StringComparison.Ordinal),
        "Portfolio validation did not include every reference pack.");
}

static async Task CliScaffoldsIndustryPack()
{
    var root = CreateTestDirectory();
    try
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);
        var exitCode = await application.RunAsync([
            "industry-pack", "add", "--name", "Publishing", "--output", root,
            "--display-name", "Publishing Pack", "--description", "Publishing extensions."
        ]);

        var packPath = Path.Combine(root, "Publishing");
        Assert(exitCode == 0 && File.Exists(Path.Combine(packPath, "pack.json")), error.ToString());
        output.GetStringBuilder().Clear();
        Assert(await application.RunAsync(["industry-pack", "validate", "--path", packPath]) == 0,
            error.ToString() + output);
        Assert(await application.RunAsync([
            "industry-pack", "add", "--name", "Publishing", "--output", root
        ]) == 2, "Scaffolding should not overwrite an existing pack.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task CliProducesLegacyDryRunReport()
{
    var root = CreateTestDirectory();
    try
    {
        var csvPath = Path.Combine(root, "legacy.csv");
        var assetsPath = Path.Combine(root, "assets.txt");
        var reportPath = Path.Combine(root, "reports", "dry-run.json");
        await File.WriteAllTextAsync(csvPath, """
            source_id,title,slug,summary,body,old_url,asset_paths
            legacy-1,Welcome,welcome,Summary,Body,/old/welcome,/assets/welcome.pdf
            """);
        await File.WriteAllTextAsync(assetsPath, "# exported asset inventory\n/assets/welcome.pdf\n");
        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);

        var exitCode = await application.RunAsync([
            "import", "legacy", "--input", csvPath, "--output", reportPath,
            "--source", "legacy-mvc", "--import-batch", "batch-20260713",
            "--as-of", "2026-07-12", "--schema-version", "legacy-content.v1",
            "--assets", assetsPath
        ]);

        Assert(exitCode == 0, error.ToString());
        var report = await File.ReadAllTextAsync(reportPath);
        Assert(report.Contains("\"source\": \"legacy-mvc\"", StringComparison.Ordinal), "Source traceability is missing.");
        Assert(report.Contains("\"checksum\":", StringComparison.Ordinal), "Checksum traceability is missing.");
        Assert(report.Contains("\"canApply\": true", StringComparison.Ordinal), "Apply decision is missing.");
        Assert(await application.RunAsync([
            "import", "legacy", "--input", csvPath, "--output", reportPath,
            "--source", "legacy-mvc", "--import-batch", "batch-20260713",
            "--as-of", "2026-07-12", "--schema-version", "legacy-content.v1"
        ]) == 2, "The CLI should not overwrite an existing report.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task CliBlocksUnsafeLegacyRows()
{
    var root = CreateTestDirectory();
    try
    {
        var csvPath = Path.Combine(root, "unsafe.csv");
        var reportPath = Path.Combine(root, "unsafe-report.json");
        await File.WriteAllTextAsync(csvPath, """
            source_id,title,slug,summary,body,old_url,asset_paths
            legacy-1,Welcome,Not Canonical,Summary,Body,,/assets/missing.pdf
            """);
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await new CliApplication(output, error).RunAsync([
            "import", "legacy", "--input", csvPath, "--output", reportPath,
            "--source", "legacy", "--import-batch", "unsafe", "--as-of", "2026-07-12",
            "--schema-version", "legacy-content.v1"
        ]);

        Assert(exitCode == 1 && File.Exists(reportPath), "Unsafe rows should produce a failed report.");
        var report = await File.ReadAllTextAsync(reportPath);
        Assert(report.Contains("slug_not_canonical", StringComparison.Ordinal) &&
               report.Contains("asset_missing", StringComparison.Ordinal), "Expected migration issues are missing.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static string CreateTestDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "opk-cli-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "OpenPortalKit.sln"))) return current.FullName;
        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

file sealed class AgentReadyHandler(bool crossOriginContent = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var response = path switch
        {
            "/robots.txt" => Response("text/plain", "User-agent: *\nSitemap: https://portal.test/sitemap.xml"),
            "/sitemap.xml" => Response("application/xml", "<urlset><url><loc>https://portal.test/content/welcome</loc></url></urlset>"),
            "/rss.xml" => Response("application/rss+xml", "<rss version=\"2.0\"><channel /></rss>"),
            "/llms.txt" => Response("text/plain", "# Portal"),
            "/.well-known/agent.json" => Response("application/json", "{\"name\":\"Portal\"}"),
            "/api/openapi.json" => Response("application/json", "{\"openapi\":\"3.1.0\"}"),
            "/api/public/content" => Response("application/json", crossOriginContent
                ? """{"items":[{"canonicalUrl":"http://127.0.0.1/private","markdownSnapshot":"https://portal.test/content/welcome.md","jsonSnapshot":"https://portal.test/api/public/content/welcome.json"}]}"""
                : """{"items":[{"canonicalUrl":"https://portal.test/content/welcome","markdownSnapshot":"https://portal.test/content/welcome.md","jsonSnapshot":"https://portal.test/api/public/content/welcome.json"}]}"""),
            "/content/welcome" => Response("text/html", "<html><head><link rel=\"canonical\"><script type=\"application/ld+json\">{}</script></head><body><main>Welcome</main></body></html>"),
            "/content/welcome.md" => Response("text/markdown", "# Welcome"),
            "/api/public/content/welcome.json" => Response("application/json", "{\"title\":\"Welcome\"}"),
            "/api/public/datasets" => Response("application/json", "[{\"code\":\"sample\"}]"),
            "/api/public/datasets/sample/schema" => Response("application/json", "{\"version\":\"1\"}"),
            "/api/public/datasets/sample/records" => Response("application/json", "{\"items\":[]}"),
            "/api/public/datasets/sample/export.csv" => Response("text/csv", "id\n1"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
        if (path.StartsWith("/api/", StringComparison.Ordinal))
            response.Headers.TryAddWithoutValidation("X-OpenPortalKit-Contract-Version", "1.0.0");
        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    private static HttpResponseMessage Response(string mediaType, string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, mediaType)
    };
}
