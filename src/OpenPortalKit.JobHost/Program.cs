using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Persistence;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Seo.Revalidation;

var builder = Host.CreateApplicationBuilder(args);
DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

var persistenceOptions = builder.Configuration
    .GetSection(PostgresPersistenceOptions.SectionName)
    .Get<PostgresPersistenceOptions>() ?? new PostgresPersistenceOptions();
if (string.IsNullOrWhiteSpace(persistenceOptions.ConnectionString))
{
    persistenceOptions.ConnectionString = builder.Configuration.GetConnectionString(
        persistenceOptions.ConnectionStringName);
}

var agentOutputOptions = builder.Configuration
    .GetSection(AgentOutputPostgresStorageOptions.SectionName)
    .Get<AgentOutputPostgresStorageOptions>() ?? new AgentOutputPostgresStorageOptions();
if (string.IsNullOrWhiteSpace(agentOutputOptions.ConnectionString))
{
    agentOutputOptions.ConnectionString = builder.Configuration.GetConnectionString(
        agentOutputOptions.ConnectionStringName);
}

if (!persistenceOptions.Enabled || !agentOutputOptions.Enabled)
{
    throw new InvalidOperationException(
        "JobHost requires OpenPortalKit:Persistence:PostgreSQL and OpenPortalKit:AgentAccess:PostgreSQL to be enabled.");
}

var outputGenerationOptions = builder.Configuration
    .GetSection(AgentOutputGenerationOptions.SectionName)
    .Get<AgentOutputGenerationOptions>() ?? new AgentOutputGenerationOptions();
var workerOptions = builder.Configuration
    .GetSection(OutboxWorkerOptions.SectionName)
    .Get<OutboxWorkerOptions>() ?? new OutboxWorkerOptions();
workerOptions.Validate();

builder.Services.AddSingleton(persistenceOptions);
builder.Services.AddSingleton(agentOutputOptions);
builder.Services.AddSingleton(outputGenerationOptions);
builder.Services.AddSingleton(workerOptions);
builder.Services.AddSingleton<IOpenPortalKitDbConnectionFactory, PostgresOpenPortalKitDbConnectionFactory>();
builder.Services.AddSingleton<IOutboxMessageStore, PostgresOutboxMessageStore>();
builder.Services.AddSingleton<IIdempotencyStore, PostgresIdempotencyStore>();
builder.Services.AddSingleton<IAuditLogStore, PostgresAuditLogStore>();
builder.Services.AddSingleton<IPublicOutputRevalidationStore, PostgresPublicOutputRevalidationStore>();
builder.Services.AddSingleton<IAgentOutputDbConnectionFactory, AgentOutputPostgresConnectionFactory>();
builder.Services.AddSingleton<IAgentOutputArtifactStore, PostgresAgentOutputArtifactStore>();
builder.Services.AddSingleton<AuditRecorder>();
builder.Services.AddSingleton<IAgentContentDocumentResolver, PublishingEventAgentContentDocumentResolver>();
builder.Services.AddSingleton<PublishingAgentOutputArtifactFactory>();
builder.Services.AddSingleton<IPublicOutputRegenerator>(provider =>
    new AgentOutputArtifactRegenerator(
        provider.GetRequiredService<IAgentOutputArtifactStore>(),
        provider.GetRequiredService<PublishingAgentOutputArtifactFactory>().CreateArtifactsAsync));
builder.Services.AddSingleton<IPublicOutputRevalidationExecutor>(provider =>
    new RecordingPublicOutputRevalidationExecutor(
        provider.GetRequiredService<IPublicOutputRevalidationStore>(),
        auditRecorder: provider.GetRequiredService<AuditRecorder>(),
        regenerator: provider.GetRequiredService<IPublicOutputRegenerator>()));
builder.Services.AddSingleton<PublishingRevalidationPlanner>();
builder.Services.AddSingleton<IOutboxMessageHandler, PublishingRevalidationOutboxHandler>();
builder.Services.AddSingleton<OutboxProcessor>();
builder.Services.AddHostedService<PublishingOutboxWorker>();

await builder.Build().RunAsync();

internal sealed class OutboxWorkerOptions
{
    public const string SectionName = "OpenPortalKit:Jobs:Outbox";

    public int BatchSize { get; set; } = 20;

    public int PollIntervalSeconds { get; set; } = 5;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PollIntervalSeconds);
    }
}

internal sealed class PublishingOutboxWorker : BackgroundService
{
    private readonly OutboxProcessor _processor;
    private readonly OutboxWorkerOptions _options;
    private readonly ILogger<PublishingOutboxWorker> _logger;

    public PublishingOutboxWorker(
        OutboxProcessor processor,
        OutboxWorkerOptions options,
        ILogger<PublishingOutboxWorker> logger)
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
                OutboxProcessingResult result;
                do
                {
                    result = await _processor.ProcessPendingAsync(_options.BatchSize, stoppingToken);
                    if (result.TotalCount > 0)
                    {
                        _logger.LogInformation(
                            "Processed publishing outbox batch: {Processed} succeeded, {Failed} failed, {Skipped} skipped.",
                            result.ProcessedCount,
                            result.FailedCount,
                            result.SkippedCount);
                    }
                }
                while (result.ProcessedCount == _options.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Publishing outbox worker failed while polling for messages.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }
}
