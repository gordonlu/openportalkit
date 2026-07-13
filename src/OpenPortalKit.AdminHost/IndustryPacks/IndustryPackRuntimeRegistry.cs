using System.Text.Json;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.AdminHost.IndustryPacks;

public sealed class IndustryPackRuntimeRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, JsonElement> _resources = new(StringComparer.OrdinalIgnoreCase);

    public void Upsert(string packName, IndustryPackResource resource)
    {
        lock (_gate)
        {
            _resources[$"{packName}:{resource.Kind}:{resource.RelativePath}"] = resource.Document.Clone();
        }
    }

    public IReadOnlyList<JsonElement> List(string packName, IndustryPackResourceKind kind)
    {
        var prefix = $"{packName}:{kind}:";
        lock (_gate)
        {
            return _resources.Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value.Clone()).ToArray();
        }
    }

    public IReadOnlyList<IndustryPackRuntimeContribution> List(IndustryPackResourceKind kind)
    {
        lock (_gate)
        {
            return _resources
                .Where(pair => pair.Key.Contains($":{kind}:", StringComparison.Ordinal))
                .Select(pair => new IndustryPackRuntimeContribution(
                    pair.Key.Split(':', 2)[0], kind, pair.Value.Clone()))
                .ToArray();
        }
    }

    public void Remove(string packName, IndustryPackResourceKind kind)
    {
        var prefix = $"{packName}:{kind}:";
        lock (_gate)
        {
            foreach (var key in _resources.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _resources.Remove(key);
            }
        }
    }
}

public sealed record IndustryPackRuntimeContribution(
    string PackName,
    IndustryPackResourceKind Kind,
    JsonElement Document);
