using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;

namespace OpenPortalKit.Cli.Checks;

public sealed class HttpAgentReadinessChecker(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<CheckReport> RunAsync(Uri baseUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri || baseUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The site URL must be an absolute HTTP or HTTPS URL.", nameof(baseUri));
        }

        baseUri = new Uri(baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/");
        var results = new List<CheckResult>();

        await CheckTextAsync(results, "OPK-AGT-101", baseUri, "robots.txt", "text/plain", "Sitemap:", cancellationToken);
        await CheckTextAsync(results, "OPK-AGT-102", baseUri, "sitemap.xml", "xml", "<urlset", cancellationToken);
        await CheckTextAsync(results, "OPK-AGT-103", baseUri, "rss.xml", "rss", "<rss", cancellationToken);
        await CheckTextAsync(results, "OPK-AGT-104", baseUri, "llms.txt", "text/plain", "#", cancellationToken);
        await CheckJsonAsync(results, "OPK-AGT-105", baseUri, ".well-known/agent.json", requireContractVersion: false, cancellationToken);
        await CheckJsonAsync(results, "OPK-AGT-106", baseUri, "api/openapi.json", requireContractVersion: true, cancellationToken);
        await CheckContentOutputsAsync(results, baseUri, cancellationToken);
        await CheckDatasetOutputsAsync(results, baseUri, cancellationToken);

        return new CheckReport("OpenPortalKit live AgentSEO readiness", results);
    }

    private async Task CheckContentOutputsAsync(
        ICollection<CheckResult> results,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "api/public/content?limit=1"), cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || !HasContractVersion(response))
            {
                results.Add(Fail("OPK-AGT-107", "content outputs", "Content discovery failed or omitted the public contract version header."));
                return;
            }

            using var document = JsonDocument.Parse(payload);
            var items = FindProperty(document.RootElement, "items");
            if (items is null || items.Value.ValueKind != JsonValueKind.Array || items.Value.GetArrayLength() == 0)
            {
                results.Add(Warn("OPK-AGT-107", "content outputs", "No published content was available for live snapshot validation."));
                return;
            }

            var item = items.Value[0];
            var canonical = ReadUri(item, "canonicalUrl");
            var markdown = ReadUri(item, "markdownSnapshot");
            var json = ReadUri(item, "jsonSnapshot");
            if (canonical is null || markdown is null || json is null)
            {
                results.Add(Fail("OPK-AGT-107", "content outputs", "Content discovery omitted canonical or snapshot URLs."));
                return;
            }
            if (!IsSameOrigin(baseUri, canonical) || !IsSameOrigin(baseUri, markdown) || !IsSameOrigin(baseUri, json))
            {
                results.Add(Fail("OPK-AGT-107", "content outputs", "Content discovery returned a cross-origin representation URL."));
                return;
            }

            using var htmlResponse = await _httpClient.GetAsync(canonical, cancellationToken);
            using var markdownResponse = await _httpClient.GetAsync(markdown, cancellationToken);
            using var jsonResponse = await _httpClient.GetAsync(json, cancellationToken);
            var html = await htmlResponse.Content.ReadAsStringAsync(cancellationToken);
            var htmlReady = htmlResponse.IsSuccessStatusCode &&
                MediaTypeContains(htmlResponse.Content.Headers.ContentType, "text/html") &&
                html.Contains("rel=\"canonical\"", StringComparison.OrdinalIgnoreCase) &&
                html.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase) &&
                html.Contains("<main", StringComparison.OrdinalIgnoreCase);
            var snapshotsReady = markdownResponse.IsSuccessStatusCode &&
                MediaTypeContains(markdownResponse.Content.Headers.ContentType, "text/markdown") &&
                jsonResponse.IsSuccessStatusCode &&
                MediaTypeContains(jsonResponse.Content.Headers.ContentType, "application/json") &&
                HasContractVersion(jsonResponse);

            results.Add(htmlReady && snapshotsReady
                ? Pass("OPK-AGT-107", "content outputs", "HTML, canonical metadata, JSON-LD, Markdown, and JSON snapshots are reachable.")
                : Fail("OPK-AGT-107", "content outputs", "One or more public content representations failed semantic or media-type validation."));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            results.Add(Fail("OPK-AGT-107", "content outputs", exception.Message));
        }
    }

    private async Task CheckDatasetOutputsAsync(
        ICollection<CheckResult> results,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "api/public/datasets"), cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || !HasContractVersion(response))
            {
                results.Add(Fail("OPK-AGT-108", "dataset outputs", "Dataset discovery failed or omitted the public contract version header."));
                return;
            }

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                results.Add(Warn("OPK-AGT-108", "dataset outputs", "No public dataset was available for live validation."));
                return;
            }

            var code = ReadString(document.RootElement[0], "code");
            if (string.IsNullOrWhiteSpace(code))
            {
                results.Add(Fail("OPK-AGT-108", "dataset outputs", "Dataset discovery omitted its code."));
                return;
            }

            var escaped = Uri.EscapeDataString(code);
            using var schema = await _httpClient.GetAsync(new Uri(baseUri, $"api/public/datasets/{escaped}/schema"), cancellationToken);
            using var records = await _httpClient.GetAsync(new Uri(baseUri, $"api/public/datasets/{escaped}/records?limit=1"), cancellationToken);
            using var export = await _httpClient.GetAsync(new Uri(baseUri, $"api/public/datasets/{escaped}/export.csv"), cancellationToken);
            var ready = schema.IsSuccessStatusCode && MediaTypeContains(schema.Content.Headers.ContentType, "application/json") && HasContractVersion(schema) &&
                records.IsSuccessStatusCode && MediaTypeContains(records.Content.Headers.ContentType, "application/json") && HasContractVersion(records) &&
                export.IsSuccessStatusCode && MediaTypeContains(export.Content.Headers.ContentType, "text/csv") && HasContractVersion(export);

            results.Add(ready
                ? Pass("OPK-AGT-108", "dataset outputs", "Schema, records, and CSV export endpoints are reachable.")
                : Fail("OPK-AGT-108", "dataset outputs", "One or more dataset representations failed status or media-type validation."));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            results.Add(Fail("OPK-AGT-108", "dataset outputs", exception.Message));
        }
    }

    private async Task CheckTextAsync(
        ICollection<CheckResult> results,
        string code,
        Uri baseUri,
        string path,
        string expectedMediaType,
        string marker,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, path), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var passed = response.IsSuccessStatusCode &&
                MediaTypeContains(response.Content.Headers.ContentType, expectedMediaType) &&
                body.Contains(marker, StringComparison.OrdinalIgnoreCase);
            results.Add(passed
                ? Pass(code, "/" + path, "Endpoint is reachable and its representation is valid.")
                : Fail(code, "/" + path, $"Expected success, media type containing '{expectedMediaType}', and marker '{marker}'."));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            results.Add(Fail(code, "/" + path, exception.Message));
        }
    }

    private async Task CheckJsonAsync(
        ICollection<CheckResult> results,
        string code,
        Uri baseUri,
        string path,
        bool requireContractVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, path), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var _ = JsonDocument.Parse(body);
            var passed = response.IsSuccessStatusCode &&
                MediaTypeContains(response.Content.Headers.ContentType, "application/json") &&
                (!requireContractVersion || HasContractVersion(response));
            results.Add(passed
                ? Pass(code, "/" + path, "Endpoint is reachable and contains valid JSON.")
                : Fail(code, "/" + path, "Expected valid application/json and the required public contract version header."));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            results.Add(Fail(code, "/" + path, exception.Message));
        }
    }

    private static bool MediaTypeContains(MediaTypeHeaderValue? contentType, string expected) =>
        contentType?.MediaType?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

    private static bool HasContractVersion(HttpResponseMessage response) =>
        response.Headers.TryGetValues(PublicApiContract.VersionHeaderName, out var values) &&
        values.Contains(PublicApiContract.Version, StringComparer.Ordinal);

    private static JsonElement? FindProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string name) =>
        FindProperty(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static Uri? ReadUri(JsonElement element, string name) =>
        Uri.TryCreate(ReadString(element, name), UriKind.Absolute, out var uri) ? uri : null;

    private static bool IsSameOrigin(Uri expected, Uri actual) =>
        expected.Scheme.Equals(actual.Scheme, StringComparison.OrdinalIgnoreCase) &&
        expected.IdnHost.Equals(actual.IdnHost, StringComparison.OrdinalIgnoreCase) &&
        expected.Port == actual.Port;

    private static CheckResult Pass(string code, string target, string message) =>
        new(code, CheckStatus.Passed, target, message);

    private static CheckResult Warn(string code, string target, string message) =>
        new(code, CheckStatus.Warning, target, message);

    private static CheckResult Fail(string code, string target, string message) =>
        new(code, CheckStatus.Failed, target, message);
}
