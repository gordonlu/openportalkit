using OpenPortalKit.Modules.Jobs;

Console.WriteLine("OpenPortalKit JobHost initialized.");
Console.WriteLine($"Loaded module: {JobsModule.Descriptor.Name}");
Console.WriteLine("Planned responsibilities: outbox processing, retries, indexing, snapshots, sitemap/RSS generation, and dashboard aggregation jobs.");
