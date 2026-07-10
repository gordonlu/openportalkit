namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class InMemoryPageTemplateStore : IPageTemplateStore
{
    private readonly object _gate = new();
    private readonly List<PageTemplate> _templates = new();
    private readonly List<PageTemplateVersion> _versions = new();

    public Task<PageTemplate> SaveAsync(PageTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var index = _templates.FindIndex(candidate => candidate.Id == template.Id);
            if (index >= 0)
            {
                _templates[index] = template;
            }
            else
            {
                _templates.Add(template);
            }

            _versions.Add(new PageTemplateVersion(
                template.Id,
                template.Version,
                template,
                template.UpdatedBy,
                template.UpdatedAt));
        }

        return Task.FromResult(template);
    }

    public Task<PageTemplate?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_templates.FirstOrDefault(template =>
                string.Equals(template.Code, code, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<IReadOnlyList<PageTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PageTemplate>>(_templates
                .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }
    }

    public Task<IReadOnlyList<PageTemplateVersion>> ListVersionsAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PageTemplateVersion>>(_versions
                .Where(version => version.TemplateId == templateId)
                .OrderByDescending(version => version.Version)
                .ToArray());
        }
    }
}
