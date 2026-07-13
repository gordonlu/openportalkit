using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.Modules.Migration.LegacyContent;

namespace OpenPortalKit.AdminHost.Pages.Migration;

[RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
public sealed class IndexModel : PageModel
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv", "text/plain", "application/csv", "application/vnd.ms-excel"
    };
    private readonly LegacyContentMigrationAnalyzer _analyzer;
    private readonly LegacyMigrationStagingService _stagingService;

    public IndexModel(
        LegacyContentMigrationAnalyzer analyzer,
        LegacyMigrationStagingService stagingService)
    {
        _analyzer = analyzer;
        _stagingService = stagingService;
    }

    [BindProperty, Required] public string Source { get; set; } = string.Empty;
    [BindProperty, Required] public string ImportBatch { get; set; } = string.Empty;
    [BindProperty, Required] public DateOnly AsOfDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [BindProperty, Required] public string SchemaVersion { get; set; } = "legacy-content.v1";
    [BindProperty, Required] public IFormFile? CsvFile { get; set; }
    [BindProperty] public string? AssetInventory { get; set; }
    public LegacyContentMigrationReport? Report { get; private set; }
    public IReadOnlyList<LegacyMigrationBatch> Batches { get; private set; } = [];
    [TempData] public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Batches = await _stagingService.ListAsync(cancellationToken);

    public async Task<IActionResult> OnPostAnalyzeAsync(CancellationToken cancellationToken)
    {
        Report = await AnalyzeUploadAsync(cancellationToken);
        Batches = await _stagingService.ListAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostStageAsync(CancellationToken cancellationToken)
    {
        Report = await AnalyzeUploadAsync(cancellationToken);
        if (Report is not null && ModelState.IsValid)
        {
            try
            {
                var batch = await _stagingService.StageAsync(Report, GetActorId(), cancellationToken);
                StatusMessage = $"Batch {batch.ImportBatch} is staged with checksum {batch.SourceChecksum[..12]}.";
                return RedirectToPage();
            }
            catch (InvalidOperationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
            }
        }
        Batches = await _stagingService.ListAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRollbackAsync(Guid batchId, CancellationToken cancellationToken)
    {
        try
        {
            var batch = await _stagingService.RollbackAsync(batchId, GetActorId(), cancellationToken);
            StatusMessage = $"Batch {batch.ImportBatch} was rolled back. Its trace record remains available.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetReportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _stagingService.FindAsync(batchId, cancellationToken);
        if (batch is null) return NotFound();

        Response.Headers.CacheControl = "no-store, max-age=0";
        return File(
            Encoding.UTF8.GetBytes(batch.ReportJson),
            "application/json; charset=utf-8",
            $"legacy-migration-{batch.Id:N}.json");
    }

    private async Task<LegacyContentMigrationReport?> AnalyzeUploadAsync(CancellationToken cancellationToken)
    {
        if (CsvFile is null)
        {
            ModelState.AddModelError(nameof(CsvFile), "Select a CSV file.");
        }
        else
        {
            var mediaType = CsvFile.ContentType.Split(';', 2)[0].Trim();
            if (!string.Equals(Path.GetExtension(CsvFile.FileName), ".csv", StringComparison.OrdinalIgnoreCase) ||
                !AllowedContentTypes.Contains(mediaType))
            {
                ModelState.AddModelError(nameof(CsvFile), "Only CSV text uploads are accepted.");
            }
            if (CsvFile.Length <= 0 || CsvFile.Length > MaxUploadBytes)
            {
                ModelState.AddModelError(nameof(CsvFile), "CSV files must be between 1 byte and 5 MB.");
            }
        }
        if (!ModelState.IsValid) return null;

        await using var stream = CsvFile!.OpenReadStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        string csv;
        try
        {
            csv = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            ModelState.AddModelError(nameof(CsvFile), "CSV must use valid UTF-8 encoding.");
            return null;
        }
        if (csv.IndexOf('\0') >= 0)
        {
            ModelState.AddModelError(nameof(CsvFile), "CSV contains unsupported null characters.");
            return null;
        }

        var assets = (AssetInventory ?? string.Empty).Split(
            ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _analyzer.Analyze(new LegacyContentMigrationRequest(
            Source, ImportBatch, AsOfDate, SchemaVersion, csv, assets));
    }

    private Guid GetActorId()
    {
        var identity = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "development-admin";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
