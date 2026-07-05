using OpenPortalKit.Modules.AgentAccess;
using OpenPortalKit.Modules.Assets;
using OpenPortalKit.Modules.Audit;
using OpenPortalKit.Modules.Content;
using OpenPortalKit.Modules.Dashboard;
using OpenPortalKit.Modules.Data;
using OpenPortalKit.Modules.Identity;
using OpenPortalKit.Modules.Jobs;
using OpenPortalKit.Modules.Search;
using OpenPortalKit.Modules.Seo;
using OpenPortalKit.Modules.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

var modules = new[]
{
    IdentityModule.Descriptor,
    ContentModule.Descriptor,
    AssetsModule.Descriptor,
    WorkflowModule.Descriptor,
    DataModule.Descriptor,
    SearchModule.Descriptor,
    SeoModule.Descriptor,
    AgentAccessModule.Descriptor,
    DashboardModule.Descriptor,
    AuditModule.Descriptor,
    JobsModule.Descriptor
};

app.MapGet("/", () => Results.Redirect("/health"));
app.MapHealthChecks("/health");

app.MapGet("/api/system/modules", () => modules.Select(module => new
{
    module.Name,
    module.Area,
    module.Description,
    module.OwnsBusinessState,
    module.PublicOutputs
}));

app.MapGet("/api/public", () => new
{
    Name = "OpenPortalKit Public API",
    Status = "initialized",
    Planned = new[]
    {
        "content",
        "datasets",
        "search",
        "sitemap",
        "rss",
        "markdown-snapshots",
        "json-snapshots",
        "openapi"
    }
});

app.Run();
