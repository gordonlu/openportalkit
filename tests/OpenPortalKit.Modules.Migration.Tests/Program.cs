using OpenPortalKit.Modules.Migration.LegacyContent;
using OpenPortalKit.Kernel.Audit;

var tests = new (string Name, Action Run)[]
{
    ("valid legacy CSV produces a traceable dry run", ValidCsvProducesTraceableReport),
    ("migration analysis blocks duplicates and missing assets", AnalysisBlocksUnsafeRows),
    ("migration analysis flags duplicate content", AnalysisFlagsDuplicateContent),
    ("validated migration batches stage idempotently and rollback with audit", StagingIsControlled),
    ("postgres migration preserves legacy batch traceability", PostgresMigrationPreservesTraceability)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

static void ValidCsvProducesTraceableReport()
{
    const string csv = """
        source_id,title,slug,summary,body,old_url,asset_paths
        legacy-1,"Welcome, teams",welcome-teams,Summary,"Body, with comma",/old/welcome,/assets/welcome.pdf
        """;
    var analyzedAt = new DateTimeOffset(2026, 7, 13, 4, 30, 0, TimeSpan.Zero);
    var analyzer = new LegacyContentMigrationAnalyzer(() => analyzedAt);
    var report = analyzer.Analyze(new LegacyContentMigrationRequest(
        "legacy-mvc5", "batch-20260713", new DateOnly(2026, 7, 12), "legacy-content.v1", csv,
        ["/assets/welcome.pdf"]));

    Assert(report.CanApply, "Valid dry run was blocked.");
    Assert(report.TotalRows == 1 && report.ValidRows == 1, "Dry-run counts are incorrect.");
    Assert(report.Checksum.Length == 64 && report.AnalyzedAt == analyzedAt, "Traceability is incomplete.");
    Assert(report.Redirects.Single().TargetPath == "/content/welcome-teams", "Redirect mapping is incorrect.");
    Assert(report.Candidates.Single().Body == "Body, with comma", "Quoted CSV body was not preserved.");
}

static void AnalysisBlocksUnsafeRows()
{
    const string csv = """
        source_id,title,slug,summary,body,old_url,asset_paths
        legacy-1,First,duplicate,Summary,Body,/old/shared,/assets/missing.pdf
        legacy-1,Second,duplicate,Summary,Other body,/old/shared,
        """;
    var report = new LegacyContentMigrationAnalyzer().Analyze(new LegacyContentMigrationRequest(
        "legacy", "batch", new DateOnly(2026, 7, 13), "v1", csv, Array.Empty<string>()));

    Assert(!report.CanApply && report.ValidRows == 0, "Unsafe dry run was allowed.");
    Assert(report.Issues.Any(issue => issue.Code == "asset_missing"), "Missing asset was not reported.");
    Assert(report.Issues.Count(issue => issue.Code == "source_id_duplicate") == 2, "Duplicate source IDs were not reported.");
    Assert(report.Issues.Count(issue => issue.Code == "slug_duplicate") == 2, "Duplicate slugs were not reported.");
    Assert(report.Issues.Count(issue => issue.Code == "old_url_duplicate") == 2, "Duplicate old URLs were not reported.");
}

static void AnalysisFlagsDuplicateContent()
{
    const string csv = """
        source_id,title,slug,summary,body,old_url,asset_paths
        one,Same,one,Summary,Body,,
        two,Same,two,Summary,Body,,
        """;
    var report = new LegacyContentMigrationAnalyzer().Analyze(new LegacyContentMigrationRequest(
        "legacy", "batch", new DateOnly(2026, 7, 13), "v1", csv));

    Assert(report.CanApply, "Duplicate content warning should not block an otherwise valid dry run.");
    Assert(report.Issues.Count(issue => issue.Code == "duplicate_content") == 2, "Duplicate content was not flagged.");
}

static void StagingIsControlled()
{
    var store = new InMemoryLegacyMigrationBatchStore();
    var audits = new InMemoryAuditLogStore();
    var now = new DateTimeOffset(2026, 7, 13, 6, 0, 0, TimeSpan.Zero);
    var service = new LegacyMigrationStagingService(store, new AuditRecorder(audits), () => now);
    var actor = Guid.Parse("10000000-0000-0000-0000-000000000001");
    var report = new LegacyContentMigrationReport(
        "legacy", "batch-1", new DateOnly(2026, 7, 12), "v1", new string('a', 64), now,
        1, 1, [], [], []);

    var staged = service.StageAsync(report, actor).GetAwaiter().GetResult();
    var repeated = service.StageAsync(report, actor).GetAwaiter().GetResult();
    Assert(staged.Id == repeated.Id && store.ListAsync().Result.Count == 1, "Identical staging was not idempotent.");

    var changed = report with { Checksum = new string('b', 64) };
    AssertThrows<InvalidOperationException>(() => service.StageAsync(changed, actor).GetAwaiter().GetResult());
    var blocked = report with
    {
        ImportBatch = "batch-2",
        Issues = [new LegacyMigrationIssue(2, "blocked", "Blocked", LegacyMigrationIssueSeverity.Error)]
    };
    AssertThrows<InvalidOperationException>(() => service.StageAsync(blocked, actor).GetAwaiter().GetResult());

    var rolledBack = service.RollbackAsync(staged.Id, actor).GetAwaiter().GetResult();
    Assert(rolledBack.Status == LegacyMigrationBatchStatus.RolledBack, "Staged batch was not rolled back.");
    AssertThrows<InvalidOperationException>(() => service.StageAsync(report, actor).GetAwaiter().GetResult());
    var logs = audits.FindByActorAsync(actor).Result;
    Assert(logs.Any(log => log.Action == "legacy-migration.staged") &&
           logs.Any(log => log.Action == "legacy-migration.rolled-back"), "Staging audit trail is incomplete.");
}

static void PostgresMigrationPreservesTraceability()
{
    const string migrationName = "0014_legacy_migration_staging.sql";
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null && !File.Exists(Path.Combine(current.FullName, "OpenPortalKit.sln")))
        current = current.Parent;
    if (current is null) throw new DirectoryNotFoundException("Repository root was not found.");

    var sql = File.ReadAllText(Path.Combine(current.FullName, "db", "postgresql", "migrations", migrationName));
    Assert(sql.Contains("legacy_migration_batches", StringComparison.OrdinalIgnoreCase), "Migration table is missing.");
    Assert(sql.Contains("source text", StringComparison.OrdinalIgnoreCase), "Source traceability is missing.");
    Assert(sql.Contains("import_batch", StringComparison.OrdinalIgnoreCase), "Import batch traceability is missing.");
    Assert(sql.Contains("as_of_date", StringComparison.OrdinalIgnoreCase), "As-of traceability is missing.");
    Assert(sql.Contains("source_checksum", StringComparison.OrdinalIgnoreCase), "Checksum traceability is missing.");
    Assert(sql.Contains("schema_version", StringComparison.OrdinalIgnoreCase), "Schema version traceability is missing.");
    Assert(sql.Contains("staged_at", StringComparison.OrdinalIgnoreCase), "Staging timestamp traceability is missing.");
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
