using OpenPortalKit.Modules.Data.Datasets;

var tests = new (string Name, Func<Task> Run)[]
{
    ("import creates traceable records with checksum", ImportCreatesTraceableRecordsWithChecksum),
    ("dry run validates without writing records", DryRunValidatesWithoutWritingRecords),
    ("invalid import does not corrupt existing records", InvalidImportDoesNotCorruptExistingRecords),
    ("checksum detects unchanged and updated records", ChecksumDetectsUnchangedAndUpdatedRecords),
    ("public query hides private datasets and returns traceability", PublicQueryHidesPrivateDatasetsAndReturnsTraceability),
    ("CSV parser imports quoted rows through data import service", CsvParserImportsQuotedRowsThroughDataImportService),
    ("CSV export includes traceability columns", CsvExportIncludesTraceabilityColumns),
    ("public query resolves schema and record by key", PublicQueryResolvesSchemaAndRecordByKey),
    ("snapshot generator creates stable JSON snapshot with checksum", SnapshotGeneratorCreatesStableJsonSnapshotWithChecksum)
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

static async Task ImportCreatesTraceableRecordsWithChecksum()
{
    var context = await CreateContextAsync(isPublic: true);
    var importedAt = new DateTimeOffset(2026, 7, 8, 14, 0, 0, TimeSpan.Zero);

    var result = await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "manual-upload",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[]
        {
            new DataImportRow("a", """{"name":"Alpha","rank":1}""")
        },
        ImportedAt: importedAt));

    var stored = await context.RecordStore.FindByKeyAsync(context.DataSet.Id, "a");

    Assert.True(result.Succeeded, "Expected import to succeed.");
    Assert.Equal(DataImportBatchStatus.Completed, result.Batch.Status);
    Assert.Equal("manual-upload", stored?.Source);
    Assert.Equal(context.Schema.Id, stored?.SchemaVersionId);
    Assert.Equal(result.Batch.Id, stored?.SourceBatchId);
    Assert.Equal(new DateOnly(2026, 7, 8), stored?.AsOfDate);
    Assert.Equal(importedAt, stored?.CreatedAt);
    Assert.Equal(importedAt, stored?.UpdatedAt);
    Assert.Equal("""{"name":"Alpha","rank":1}""", stored?.PayloadJson);
    Assert.True(!string.IsNullOrWhiteSpace(stored?.Checksum), "Expected checksum to be written.");
}

static async Task DryRunValidatesWithoutWritingRecords()
{
    var context = await CreateContextAsync(isPublic: true);

    var result = await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "dry-run",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("dry", """{"name":"Dry"}""") },
        DryRun: true));

    var records = await context.RecordStore.ListByDataSetAsync(context.DataSet.Id);

    Assert.True(result.Succeeded, "Expected dry run to validate.");
    Assert.True(result.DryRun, "Expected result to indicate dry run.");
    Assert.Equal(DataImportBatchStatus.DryRunSucceeded, result.Batch.Status);
    Assert.Equal(0, records.Count);
}

static async Task InvalidImportDoesNotCorruptExistingRecords()
{
    var context = await CreateContextAsync(isPublic: true);

    await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "initial",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("stable", """{"name":"Stable"}""") }));

    var result = await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "bad-upload",
        new DateOnly(2026, 7, 9),
        Guid.NewGuid(),
        new[]
        {
            new DataImportRow("stable", """{"name":"Changed"}"""),
            new DataImportRow("broken", "{not-json")
        }));

    var stored = await context.RecordStore.FindByKeyAsync(context.DataSet.Id, "stable");

    Assert.False(result.Succeeded, "Expected invalid import to fail.");
    Assert.Equal(DataImportBatchStatus.Failed, result.Batch.Status);
    Assert.Equal(1, result.QualityReport.ErrorCount);
    Assert.Equal("""{"name":"Stable"}""", stored?.PayloadJson);
}

static async Task ChecksumDetectsUnchangedAndUpdatedRecords()
{
    var context = await CreateContextAsync(isPublic: true);

    await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "initial",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[]
        {
            new DataImportRow("a", """{"rank":1,"name":"Alpha"}"""),
            new DataImportRow("b", """{"name":"Beta","rank":2}""")
        }));

    var second = await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "second",
        new DateOnly(2026, 7, 9),
        Guid.NewGuid(),
        new[]
        {
            new DataImportRow("a", """{"name":"Alpha","rank":1}"""),
            new DataImportRow("b", """{"name":"Beta","rank":3}"""),
            new DataImportRow("c", """{"name":"Gamma","rank":4}""")
        }));

    Assert.Equal(1, second.Batch.UnchangedRecords);
    Assert.Equal(1, second.Batch.UpdatedRecords);
    Assert.Equal(1, second.Batch.CreatedRecords);
}

static async Task PublicQueryHidesPrivateDatasetsAndReturnsTraceability()
{
    var publicContext = await CreateContextAsync(isPublic: true, code: "public-set");
    var privateContext = await CreateContextAsync(isPublic: false, code: "private-set");

    await publicContext.Service.ImportAsync(new DataImportRequest(
        publicContext.DataSet.Id,
        publicContext.Schema.Id,
        "public-source",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("visible", """{"name":"Visible"}""") }));
    await privateContext.Service.ImportAsync(new DataImportRequest(
        privateContext.DataSet.Id,
        privateContext.Schema.Id,
        "private-source",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("hidden", """{"name":"Hidden"}""") }));

    var publicQuery = new PublicDataSetQueryService(publicContext.DataSetStore, publicContext.RecordStore);
    var privateQuery = new PublicDataSetQueryService(privateContext.DataSetStore, privateContext.RecordStore);
    var publicDetail = await publicQuery.FindByCodeAsync(publicContext.DataSet.SiteId, "public-set");
    var privateDetail = await privateQuery.FindByCodeAsync(privateContext.DataSet.SiteId, "private-set");

    Assert.Equal("public-set", publicDetail?.Code);
    Assert.Equal(1, publicDetail?.Records.Count);
    Assert.Equal("public-source", publicDetail?.Records[0].Source);
    Assert.True(!string.IsNullOrWhiteSpace(publicDetail?.Records[0].Checksum), "Expected public records to include checksum.");
    Assert.Equal(null, privateDetail);
}

static async Task CsvParserImportsQuotedRowsThroughDataImportService()
{
    var context = await CreateContextAsync(isPublic: true);
    var parsed = CsvImportParser.Parse(""""""
        record_key,name,description
        a,Alpha,"One, quoted value"
        b,Beta,"Line ""two"""
        """""");

    var result = await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "csv-upload",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        parsed.Rows,
        SourceFileName: "sample.csv"));

    var records = await context.RecordStore.ListByDataSetAsync(context.DataSet.Id);

    Assert.Equal(0, parsed.Errors.Count);
    Assert.True(result.Succeeded, "Expected CSV import to succeed.");
    Assert.Equal(2, records.Count);
    Assert.Equal("""{"description":"One, quoted value","name":"Alpha"}""", records[0].PayloadJson);
    Assert.Equal("sample.csv", result.Batch.SourceFileName);
}

static async Task CsvExportIncludesTraceabilityColumns()
{
    var context = await CreateContextAsync(isPublic: true);

    await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "csv-export",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("a", """{"name":"Alpha"}""") },
        ImportedAt: new DateTimeOffset(2026, 7, 8, 14, 0, 0, TimeSpan.Zero)));

    var query = new PublicDataSetQueryService(context.DataSetStore, context.RecordStore);
    var detail = await query.FindByCodeAsync(context.DataSet.SiteId, context.DataSet.Code);
    var csv = CsvDataExporter.Export(detail!.Records);

    Assert.Contains("record_key,payload_json,as_of_date,schema_version_id,source_batch_id,source,checksum,updated_at", csv);
    Assert.Contains("csv-export", csv);
    Assert.Contains("2026-07-08", csv);
}

static async Task PublicQueryResolvesSchemaAndRecordByKey()
{
    var context = await CreateContextAsync(isPublic: true, code: "schema-set");

    await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "query-source",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("one", """{"name":"One"}""") }));

    var query = new PublicDataSetQueryService(context.DataSetStore, context.RecordStore);
    var schema = await query.FindSchemaByCodeAsync(context.DataSet.SiteId, "schema-set");
    var record = await query.FindRecordByKeyAsync(context.DataSet.SiteId, "schema-set", "one");

    Assert.Equal(1, schema?.VersionNumber);
    Assert.Equal(context.Schema.Checksum, schema?.Checksum);
    Assert.Equal("one", record?.RecordKey);
    Assert.Equal("query-source", record?.Source);
}

static async Task SnapshotGeneratorCreatesStableJsonSnapshotWithChecksum()
{
    var context = await CreateContextAsync(isPublic: true, code: "snapshot-set");
    var import = await context.Service.ImportAsync(new DataImportRequest(
        context.DataSet.Id,
        context.Schema.Id,
        "snapshot-source",
        new DateOnly(2026, 7, 8),
        Guid.NewGuid(),
        new[] { new DataImportRow("one", """{"name":"One"}""") }));

    var query = new PublicDataSetQueryService(context.DataSetStore, context.RecordStore);
    var detail = await query.FindByCodeAsync(context.DataSet.SiteId, "snapshot-set");
    var snapshot = DataSnapshotGenerator.CreateJsonSnapshot(
        context.DataSet.Id,
        context.Schema.Id,
        import.Batch.Id,
        detail!,
        new DateTimeOffset(2026, 7, 8, 15, 0, 0, TimeSpan.Zero));

    Assert.Equal("json", snapshot.Format);
    Assert.Contains("snapshot-set", snapshot.Content);
    Assert.Equal(import.Batch.Id, snapshot.SourceBatchId);
    Assert.True(!string.IsNullOrWhiteSpace(snapshot.Checksum), "Expected snapshot checksum.");
}

static async Task<DataTestContext> CreateContextAsync(bool isPublic, string code = "dataset")
{
    var dataSetStore = new InMemoryDataSetStore();
    var recordStore = new InMemoryDataRecordStore();
    var service = new DataImportService(dataSetStore, recordStore);
    var now = new DateTimeOffset(2026, 7, 8, 13, 0, 0, TimeSpan.Zero);
    var dataSet = new DataSet(
        Guid.NewGuid(),
        Guid.NewGuid(),
        code,
        "Dataset",
        "A generic structured dataset.",
        isPublic,
        now,
        now);
    var schema = new DataSchemaVersion(
        Guid.NewGuid(),
        dataSet.Id,
        VersionNumber: 1,
        """{"type":"object"}""",
        DataChecksum.FromJson("""{"type":"object"}"""),
        now);

    await dataSetStore.AddDataSetAsync(dataSet);
    await dataSetStore.AddSchemaVersionAsync(schema);

    return new DataTestContext(dataSetStore, recordStore, service, dataSet, schema);
}

internal sealed record DataTestContext(
    InMemoryDataSetStore DataSetStore,
    InMemoryDataRecordStore RecordStore,
    DataImportService Service,
    DataSet DataSet,
    DataSchemaVersion Schema);

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected output to contain '{expected}'.");
        }
    }
}
