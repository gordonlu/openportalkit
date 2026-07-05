using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Data;

public static class DataModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Data",
        "structured-data",
        "Datasets, schema versions, records, imports, snapshots, and public data views.",
        true,
        new[] { "HTML", "JSON", "CSV", "OpenAPI" });
}
