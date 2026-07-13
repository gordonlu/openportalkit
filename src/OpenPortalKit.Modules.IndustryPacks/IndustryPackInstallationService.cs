using System.Text.Json;
using OpenPortalKit.Kernel.Audit;

namespace OpenPortalKit.Modules.IndustryPacks;

public sealed class IndustryPackInstallationService
{
    private readonly IIndustryPackInstallationStore _store;
    private readonly IReadOnlyDictionary<IndustryPackResourceKind, IIndustryPackResourceRegistrationTarget> _targets;
    private readonly AuditRecorder _auditRecorder;
    private readonly Func<DateTimeOffset> _clock;

    public IndustryPackInstallationService(
        IIndustryPackInstallationStore store,
        IEnumerable<IIndustryPackResourceRegistrationTarget> targets,
        AuditRecorder auditRecorder,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _targets = targets.ToDictionary(target => target.Kind);
    }

    public async Task<IndustryPackRegistrationPlan> PlanAsync(
        LoadedIndustryPack pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var installation = await _store.FindAsync(pack.Manifest.Name, cancellationToken);
        var previous = (await _store.ListResourcesAsync(pack.Manifest.Name, cancellationToken))
            .ToDictionary(resource => resource.ResourcePath, StringComparer.OrdinalIgnoreCase);
        var current = pack.Resources.ToDictionary(resource => resource.RelativePath, StringComparer.OrdinalIgnoreCase);
        var changes = new List<IndustryPackResourceChange>();
        foreach (var resource in pack.Resources.OrderBy(resource => resource.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!previous.TryGetValue(resource.RelativePath, out var prior))
            {
                changes.Add(new IndustryPackResourceChange(resource.Kind, resource.RelativePath,
                    IndustryPackResourceChangeType.Add, null, resource.Checksum));
            }
            else
            {
                changes.Add(new IndustryPackResourceChange(resource.Kind, resource.RelativePath,
                    string.Equals(prior.Checksum, resource.Checksum, StringComparison.Ordinal)
                        ? IndustryPackResourceChangeType.Unchanged
                        : IndustryPackResourceChangeType.Update,
                    prior.Checksum, resource.Checksum));
            }
        }

        foreach (var removed in previous.Values.Where(item => !current.ContainsKey(item.ResourcePath)))
        {
            changes.Add(new IndustryPackResourceChange(removed.Kind, removed.ResourcePath,
                IndustryPackResourceChangeType.Remove, removed.Checksum, null));
        }

        return new IndustryPackRegistrationPlan(
            pack.Manifest.Name,
            pack.Manifest.Version,
            installation?.IsEnabled ?? false,
            installation is null || !string.Equals(installation.ManifestChecksum, pack.ManifestChecksum, StringComparison.Ordinal),
            changes);
    }

    public async Task<IndustryPackOperationResult> EnableAsync(
        LoadedIndustryPack pack,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(pack, cancellationToken);
        var errors = new List<string>();
        foreach (var resource in pack.Resources)
        {
            if (!_targets.TryGetValue(resource.Kind, out var target))
            {
                errors.Add($"No registration target is available for {resource.Kind} resources.");
                continue;
            }

            errors.AddRange(await target.ValidateAsync(pack, resource, cancellationToken));
        }

        if (errors.Count > 0)
        {
            return new IndustryPackOperationResult(false, null, plan, errors);
        }

        var changedPaths = plan.Changes
            .Where(change => change.ChangeType is IndustryPackResourceChangeType.Add or IndustryPackResourceChangeType.Update)
            .Select(change => change.ResourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in pack.Resources.Where(resource => changedPaths.Contains(resource.RelativePath)))
        {
            await _targets[resource.Kind].ApplyAsync(pack, resource, cancellationToken);
        }

        var now = _clock();
        var previous = await _store.FindAsync(pack.Manifest.Name, cancellationToken);
        var installation = new IndustryPackInstallation(
            pack.Manifest.Name,
            pack.Manifest.Version,
            pack.ManifestChecksum,
            true,
            actorId,
            previous?.InstalledAt ?? now,
            now);
        var registrations = pack.Resources.Select(resource => new IndustryPackResourceRegistration(
            pack.Manifest.Name, resource.RelativePath, resource.Kind, resource.Checksum, now)).ToArray();
        await _store.SaveAsync(installation, registrations, cancellationToken);
        await RecordAuditAsync("industry-pack.enabled", installation, plan, actorId, cancellationToken);
        return new IndustryPackOperationResult(true, installation, plan, Array.Empty<string>());
    }

    public async Task<IndustryPackOperationResult> DisableAsync(
        LoadedIndustryPack pack,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(pack, cancellationToken);
        var previous = await _store.FindAsync(pack.Manifest.Name, cancellationToken);
        if (previous is null)
        {
            return new IndustryPackOperationResult(false, null, plan, new[] { "Pack has not been installed." });
        }

        var now = _clock();
        foreach (var target in _targets.Values)
        {
            await target.DisableAsync(pack, cancellationToken);
        }

        var disabled = previous with { IsEnabled = false, UpdatedBy = actorId, UpdatedAt = now };
        var resources = await _store.ListResourcesAsync(pack.Manifest.Name, cancellationToken);
        await _store.SaveAsync(disabled, resources, cancellationToken);
        await RecordAuditAsync("industry-pack.disabled", disabled, plan, actorId, cancellationToken);
        return new IndustryPackOperationResult(true, disabled, plan, Array.Empty<string>());
    }

    public async Task<IndustryPackRehydrationResult> RehydrateEnabledAsync(
        IReadOnlyList<LoadedIndustryPack> availablePacks,
        CancellationToken cancellationToken = default)
    {
        var available = availablePacks.ToDictionary(pack => pack.Manifest.Name, StringComparer.OrdinalIgnoreCase);
        var enabled = (await _store.ListAsync(cancellationToken)).Where(item => item.IsEnabled).ToArray();
        var errors = new List<string>();
        var resourceCount = 0;
        var packCount = 0;
        foreach (var installation in enabled)
        {
            if (!available.TryGetValue(installation.PackName, out var pack))
            {
                errors.Add($"Enabled pack '{installation.PackName}' is not available on disk.");
                continue;
            }

            if (!string.Equals(installation.ManifestChecksum, pack.ManifestChecksum, StringComparison.Ordinal))
            {
                errors.Add($"Enabled pack '{installation.PackName}' changed on disk; review its dry-run before upgrading.");
                continue;
            }

            foreach (var resource in pack.Resources)
            {
                if (!_targets.TryGetValue(resource.Kind, out var target) || !target.RequiresStartupRehydration)
                {
                    continue;
                }

                var validationErrors = await target.ValidateAsync(pack, resource, cancellationToken);
                if (validationErrors.Count > 0)
                {
                    errors.AddRange(validationErrors);
                    continue;
                }

                await target.ApplyAsync(pack, resource, cancellationToken);
                resourceCount++;
            }

            packCount++;
        }

        return new IndustryPackRehydrationResult(
            packCount,
            resourceCount,
            errors);
    }

    private Task RecordAuditAsync(
        string action,
        IndustryPackInstallation installation,
        IndustryPackRegistrationPlan plan,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        return _auditRecorder.RecordAsync(new AuditRecordRequest(
            actorId,
            action,
            "IndustryPack",
            installation.PackName,
            $"{action} '{installation.PackName}' version {installation.Version}.",
            JsonSerializer.Serialize(new
            {
                installation.Version,
                installation.ManifestChecksum,
                installation.IsEnabled,
                plan.ManifestChanged,
                plan.ChangedResourceCount
            })), cancellationToken);
    }
}
