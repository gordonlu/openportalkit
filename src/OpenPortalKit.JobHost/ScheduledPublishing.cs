using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Workflow.Publishing;

internal sealed class PortalPageScheduledPublishingTarget : IScheduledPublishingTarget
{
    private readonly IPageStore _pageStore;
    private readonly PortalPageService _pageService;

    public PortalPageScheduledPublishingTarget(IPageStore pageStore, PortalPageService pageService)
    {
        _pageStore = pageStore ?? throw new ArgumentNullException(nameof(pageStore));
        _pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
    }

    public string TargetType => "PortalPage";

    public async Task<ScheduledPublishingTargetResult> PublishAsync(
        string targetId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(targetId, out var pageId))
            return ScheduledPublishingTargetResult.Failure("Scheduled portal page target identifier is invalid.");
        var page = await _pageStore.FindByIdAsync(pageId, cancellationToken);
        if (page is null)
            return ScheduledPublishingTargetResult.Failure("Scheduled portal page was not found.");
        var result = await _pageService.PublishAsync(page.SiteId, page.Slug, actorId, cancellationToken);
        return result.Succeeded
            ? ScheduledPublishingTargetResult.Success()
            : ScheduledPublishingTargetResult.Failure(result.Errors.ToArray());
    }
}

internal sealed class ScheduledPublishingWorkerOptions
{
    public const string SectionName = "OpenPortalKit:Jobs:ScheduledPublishing";

    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 15;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BatchSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BatchSize, 1000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PollIntervalSeconds);
    }
}

internal sealed class ScheduledPublishingWorker : BackgroundService
{
    private static readonly Guid SystemActorId =
        Guid.Parse("a2000000-0000-0000-0000-000000000002");
    private readonly ScheduledPublishingProcessor _processor;
    private readonly ScheduledPublishingWorkerOptions _options;
    private readonly ILogger<ScheduledPublishingWorker> _logger;

    public ScheduledPublishingWorker(
        ScheduledPublishingProcessor processor,
        ScheduledPublishingWorkerOptions options,
        ILogger<ScheduledPublishingWorker> logger)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _processor.ProcessDueAsync(
                    DateTimeOffset.UtcNow, _options.BatchSize, SystemActorId, stoppingToken);
                if (result.DueCount > 0)
                {
                    _logger.LogInformation(
                        "Processed scheduled publishing batch: {Published} published, {Skipped} concurrent skips, {Failed} failed.",
                        result.PublishedCount,
                        result.SkippedCount,
                        result.Failures.Count);
                    foreach (var failure in result.Failures)
                    {
                        _logger.LogError(
                            "Scheduled publishing failed for {TargetType} {TargetId}: {Error}",
                            failure.TargetType,
                            failure.TargetId,
                            failure.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Scheduled publishing worker failed while polling for due items.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
