using OpenPortalKit.Kernel.Configuration;
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

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.Configure<OpenPortalKitStorageOptions>(
    builder.Configuration.GetSection(OpenPortalKitStorageOptions.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapGet("/admin/system/modules", () => new[]
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
});
app.MapGet("/admin/system/storage", (IConfiguration configuration) =>
{
    var storage = configuration
        .GetSection(OpenPortalKitStorageOptions.SectionName)
        .Get<OpenPortalKitStorageOptions>() ?? new OpenPortalKitStorageOptions();

    return new
    {
        storage.Provider,
        storage.PrimaryConnectionStringName,
        PrimaryConnectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString(storage.PrimaryConnectionStringName)),
        storage.CacheConnectionStringName,
        CacheConnectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString(storage.CacheConnectionStringName))
    };
});
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
