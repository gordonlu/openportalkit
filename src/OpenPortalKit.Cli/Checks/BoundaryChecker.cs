using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenPortalKit.Cli.Checks;

public sealed class BoundaryChecker
{
    private static readonly string[] ForbiddenIndustryTerms =
    [
        "Fund", "IPO", "Stock", "FinancialSecurity", "SecuritySnapshot", "Broker", "Finance",
        "MarketCommentary", "RiskDisclosure", "FundNav", "IpoProject", "Course", "Student",
        "Talent", "Streaming"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedModuleDependencies =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["OpenPortalKit.Modules.AgentAccess"] = Set("OpenPortalKit.Modules.Seo"),
            ["OpenPortalKit.Modules.Dashboard"] = Set(
                "OpenPortalKit.Modules.Content",
                "OpenPortalKit.Modules.Data"),
            ["OpenPortalKit.Modules.Migration"] = Set(
                "OpenPortalKit.Modules.Content",
                "OpenPortalKit.Modules.Data",
                "OpenPortalKit.Modules.Seo")
        };

    private static readonly string[] AdminOnlyIdentifiers =
    [
        "PasswordHash", "SecurityStamp", "RefreshToken", "ClientSecret"
    ];

    public CheckReport Run(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        root = Path.GetFullPath(root);

        var results = new List<CheckResult>();
        CheckIndustryNeutrality(root, results);
        CheckProjectDependencies(root, results);
        CheckPublicApiSurface(root, results);
        CheckMigrationCoverage(root, results);

        return new CheckReport("OpenPortalKit boundary check", results);
    }

    private static void CheckIndustryNeutrality(string root, ICollection<CheckResult> results)
    {
        var sourceRoot = Path.Combine(root, "src");
        if (!Directory.Exists(sourceRoot))
        {
            results.Add(Fail("OPK-BND-001", "src", "Core source directory was not found."));
            return;
        }

        var coreDirectories = Directory.EnumerateDirectories(sourceRoot, "OpenPortalKit.*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path) == "OpenPortalKit.Kernel" ||
                           Path.GetFileName(path).StartsWith("OpenPortalKit.Modules.", StringComparison.Ordinal))
            .ToArray();
        var files = coreDirectories.SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        var violations = new List<string>();

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var term in ForbiddenIndustryTerms)
            {
                if (Regex.IsMatch(
                        content,
                        $@"(?<![A-Za-z0-9_]){Regex.Escape(term)}(?![A-Za-z0-9_])",
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                {
                    violations.Add($"{Path.GetRelativePath(root, file)} ({term})");
                }
            }
        }

        results.Add(violations.Count == 0
            ? Pass("OPK-BND-001", "src", "Core and generic modules are industry-neutral.")
            : Fail("OPK-BND-001", "src", "Industry-specific terms found: " + string.Join(", ", violations)));
    }

    private static void CheckProjectDependencies(string root, ICollection<CheckResult> results)
    {
        var projectFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        var violations = new List<string>();

        foreach (var projectFile in projectFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            var references = ReadProjectReferences(projectFile);

            if (projectName.StartsWith("OpenPortalKit.Modules.", StringComparison.Ordinal))
            {
                foreach (var reference in references)
                {
                    if (reference is "OpenPortalKit.Kernel") continue;
                    if (AllowedModuleDependencies.TryGetValue(projectName, out var allowed) && allowed.Contains(reference))
                        continue;

                    violations.Add($"{projectName} -> {reference}");
                }
            }

            if (references.Any(reference => reference.EndsWith("Host", StringComparison.Ordinal)))
            {
                violations.Add($"{projectName} -> host project");
            }

            var document = XDocument.Load(projectFile);
            foreach (var include in document.Descendants("ProjectReference")
                         .Select(element => (string?)element.Attribute("Include")))
            {
                if (include is null) continue;
                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, include));
                if (IsUnderDirectory(resolved, Path.Combine(root, "industry-packs")))
                {
                    violations.Add($"{projectName} -> {Path.GetRelativePath(root, resolved)}");
                }
            }
        }

        results.Add(violations.Count == 0
            ? Pass("OPK-BND-002", "project references", "Module dependencies follow documented directions.")
            : Fail("OPK-BND-002", "project references", "Forbidden dependencies found: " + string.Join(", ", violations)));
    }

    private static void CheckPublicApiSurface(string root, ICollection<CheckResult> results)
    {
        var apiRoot = Path.Combine(root, "src", "OpenPortalKit.ApiHost");
        if (!Directory.Exists(apiRoot))
        {
            results.Add(Fail("OPK-BND-003", "ApiHost", "Public API host was not found."));
            return;
        }

        var source = string.Join('\n', Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(File.ReadAllText));
        var leaked = AdminOnlyIdentifiers.Where(identifier =>
            Regex.IsMatch(source, $@"\b{Regex.Escape(identifier)}\b", RegexOptions.CultureInvariant)).ToArray();

        results.Add(leaked.Length == 0
            ? Pass("OPK-BND-003", "ApiHost", "Public API source does not reference known credential fields.")
            : Fail("OPK-BND-003", "ApiHost", "Credential-oriented identifiers found: " + string.Join(", ", leaked)));
    }

    private static void CheckMigrationCoverage(string root, ICollection<CheckResult> results)
    {
        var migrationRoot = Path.Combine(root, "db", "postgresql", "migrations");
        var testRoot = Path.Combine(root, "tests");
        if (!Directory.Exists(migrationRoot) || !Directory.Exists(testRoot))
        {
            results.Add(Fail("OPK-BND-004", "PostgreSQL migrations", "Migration or test directory was not found."));
            return;
        }

        var testContent = string.Join('\n', Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(File.ReadAllText));
        var uncovered = Directory.EnumerateFiles(migrationRoot, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !testContent.Contains(name, StringComparison.Ordinal))
            .ToArray();

        results.Add(uncovered.Length == 0
            ? Pass("OPK-BND-004", "PostgreSQL migrations", "Every incremental migration is referenced by a test.")
            : Fail("OPK-BND-004", "PostgreSQL migrations", "Migrations without explicit test coverage: " + string.Join(", ", uncovered)));
    }

    private static IReadOnlyList<string> ReadProjectReferences(string projectFile)
    {
        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToArray();
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static bool IsUnderDirectory(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static CheckResult Pass(string code, string target, string message) =>
        new(code, CheckStatus.Passed, target, message);

    private static CheckResult Fail(string code, string target, string message) =>
        new(code, CheckStatus.Failed, target, message);
}
