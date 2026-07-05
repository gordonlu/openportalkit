namespace OpenPortalKit.Kernel.Modules;

public sealed record OpenPortalKitModuleDescriptor(
    string Name,
    string Area,
    string Description,
    bool OwnsBusinessState,
    IReadOnlyList<string> PublicOutputs);
