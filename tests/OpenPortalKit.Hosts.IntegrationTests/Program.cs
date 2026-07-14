using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenPortalKit.Modules.Identity.Authentication;

var tests = new (string Name, Func<Task> Run)[]
{
    ("ApiHost preserves public and security contracts", ApiHostPreservesPublicAndSecurityContracts),
    ("AdminHost enforces authentication CSRF and secure cookies", AdminHostEnforcesAuthenticationCsrfAndSecureCookies)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static async Task ApiHostPreservesPublicAndSecurityContracts()
{
    var root = FindRepositoryRoot();
    await using var host = await TestHost.StartAsync(root, "OpenPortalKit.ApiHost", new Dictionary<string, string?>());
    using var client = CreateClient(host.BaseUri);

    using var openApi = await client.GetAsync("api/openapi.json");
    Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
    Assert.MediaType("application/json", openApi.Content.Headers.ContentType?.MediaType);
    Assert.Header(openApi, "X-OpenPortalKit-Contract-Version", "1.0.0");
    Assert.SecurityHeaders(openApi);
    using (var document = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync()))
    {
        Assert.Equal("1.0.0", document.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/content/{slug}", out _),
            "OpenAPI omitted the public HTML content route.");
    }

    using var contentList = await client.GetAsync("api/public/content?limit=1");
    Assert.Equal(HttpStatusCode.OK, contentList.StatusCode);
    Assert.Header(contentList, "X-OpenPortalKit-Contract-Version", "1.0.0");
    using var contentDocument = JsonDocument.Parse(await contentList.Content.ReadAsStringAsync());
    var item = contentDocument.RootElement.GetProperty("items")[0];
    var canonical = new Uri(item.GetProperty("canonicalUrl").GetString()!);

    using var html = await client.GetAsync(canonical.PathAndQuery);
    Assert.Equal(HttpStatusCode.OK, html.StatusCode);
    Assert.MediaType("text/html", html.Content.Headers.ContentType?.MediaType);
    Assert.SecurityHeaders(html);
    Assert.True(html.Headers.ETag is not null, "Public HTML did not expose an ETag.");
    Assert.True(html.Headers.CacheControl?.Public == true, "Public HTML is not cacheable.");
    var body = await html.Content.ReadAsStringAsync();
    Assert.Contains("<main>", body);
    Assert.Contains("rel=\"canonical\"", body);
    Assert.Contains("application/ld+json", body);

    using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, canonical.PathAndQuery);
    conditionalRequest.Headers.IfNoneMatch.Add(html.Headers.ETag!);
    using var notModified = await client.SendAsync(conditionalRequest);
    Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

    using var invalidPage = await client.GetAsync("api/public/content?limit=101");
    Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
    Assert.Header(invalidPage, "X-OpenPortalKit-Contract-Version", "1.0.0");

    using var pageList = await client.GetAsync("api/public/pages?limit=1");
    Assert.Equal(HttpStatusCode.OK, pageList.StatusCode);
    Assert.Header(pageList, "X-OpenPortalKit-Contract-Version", "1.0.0");
    using (var pageDocument = JsonDocument.Parse(await pageList.Content.ReadAsStringAsync()))
    {
        var publicPage = pageDocument.RootElement.GetProperty("items")[0];
        Assert.True(publicPage.TryGetProperty("canonicalUrl", out _), "Public page summary omitted canonical URL.");
        Assert.True(publicPage.TryGetProperty("markdownSnapshot", out _), "Public page summary omitted Markdown URL.");
        Assert.False(publicPage.TryGetProperty("blocks", out _), "Public page summary leaked block configuration.");
        Assert.False(publicPage.TryGetProperty("updatedBy", out _), "Public page summary leaked an actor identifier.");
    }

    using var unsafeMethod = await client.PostAsync("api/public/content", new StringContent("{}"));
    Assert.Equal(HttpStatusCode.MethodNotAllowed, unsafeMethod.StatusCode);
    Assert.Header(unsafeMethod, "X-OpenPortalKit-Contract-Version", "1.0.0");

    using var dataSets = await client.GetAsync("api/public/datasets");
    Assert.Equal(HttpStatusCode.OK, dataSets.StatusCode);
    using (var dataSetDocument = JsonDocument.Parse(await dataSets.Content.ReadAsStringAsync()))
    {
        var dataSet = dataSetDocument.RootElement[0];
        Assert.Equal("sample-catalog", dataSet.GetProperty("code").GetString());
        Assert.True(dataSet.TryGetProperty("updatedAt", out _), "Public dataset summary omitted freshness metadata.");
    }

    using var search = await client.GetAsync("api/public/search?q=Public%20Pages");
    Assert.Equal(HttpStatusCode.OK, search.StatusCode);
    using (var searchDocument = JsonDocument.Parse(await search.Content.ReadAsStringAsync()))
    {
        var pageResult = searchDocument.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("targetType").GetString() == "PortalPage");
        Assert.Equal("OpenPortalKit Public Pages", pageResult.GetProperty("title").GetString());
        Assert.Equal("/pages/public-pages", pageResult.GetProperty("url").GetString());
    }

    using var traceRequest = new HttpRequestMessage(HttpMethod.Get, "health/live");
    traceRequest.Headers.TryAddWithoutValidation("X-Trace-Id", "integration-trace-1234");
    using var traced = await client.SendAsync(traceRequest);
    Assert.Header(traced, "X-Trace-Id", "integration-trace-1234");
}

static async Task AdminHostEnforcesAuthenticationCsrfAndSecureCookies()
{
    const string userName = "integration-admin";
    const string password = "integration-password-42";
    var root = FindRepositoryRoot();
    var passwordHash = new PasswordCredentialHasher().Hash(password, 100_000);
    var environment = new Dictionary<string, string?>
    {
        ["OpenPortalKit__AdminAuthentication__RequireAuthentication"] = "true",
        ["OpenPortalKit__AdminAuthentication__Mode"] = "Local",
        ["OpenPortalKit__AdminAuthentication__UserName"] = userName,
        ["OpenPortalKit__AdminAuthentication__PasswordHash"] = passwordHash,
        ["OpenPortalKit__AdminAuthentication__MaxFailedAttempts"] = "5",
        ["OpenPortalKit__Production__EnableHttpsRedirection"] = "false",
        ["OpenPortalKit__Production__LoginAttemptsPerFiveMinutes"] = "20"
    };
    await using var host = await TestHost.StartAsync(root, "OpenPortalKit.AdminHost", environment);
    var cookies = new CookieContainer();
    using var client = CreateClient(host.BaseUri, cookies);

    using var anonymous = await client.GetAsync("");
    Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
    var loginLocation = anonymous.Headers.Location is null
        ? null
        : new Uri(host.BaseUri, anonymous.Headers.Location).AbsolutePath;
    Assert.Equal("/Account/Login", loginLocation);
    Assert.SecurityHeaders(anonymous);

    var loginPage = await GetLoginPageAsync(client, "https://attacker.example/steal");
    using var missingCsrf = await client.PostAsync("Account/Login", Form(userName, password, string.Empty, "/"));
    Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

    using var invalid = await client.PostAsync(
        "Account/Login",
        Form(userName, "wrong-password", loginPage.Token, "/"));
    Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
    Assert.Contains("The supplied administrator credentials are invalid.", await invalid.Content.ReadAsStringAsync());
    Assert.False(HasAuthenticationCookie(invalid), "Failed login issued an authentication cookie.");

    loginPage = await GetLoginPageAsync(client, "https://attacker.example/steal");
    using var valid = await client.PostAsync(
        "Account/Login",
        Form(userName, password, loginPage.Token, "https://attacker.example/steal"));
    Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
    Assert.Equal("/", valid.Headers.Location?.OriginalString);
    var authCookie = valid.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith("__Host-OpenPortalKit.Admin=", StringComparison.Ordinal));
    Assert.Contains("path=/", authCookie.ToLowerInvariant());
    Assert.Contains("secure", authCookie.ToLowerInvariant());
    Assert.Contains("httponly", authCookie.ToLowerInvariant());
    Assert.Contains("samesite=strict", authCookie.ToLowerInvariant());
    Assert.False(authCookie.Contains("domain=", StringComparison.OrdinalIgnoreCase),
        "Host-only authentication cookie unexpectedly declared a Domain.");

    using var contentStudioRequest = new HttpRequestMessage(HttpMethod.Get, "Content");
    contentStudioRequest.Headers.TryAddWithoutValidation("Cookie", authCookie.Split(';', 2)[0]);
    using var contentStudio = await client.SendAsync(contentStudioRequest);
    Assert.Equal(HttpStatusCode.OK, contentStudio.StatusCode);
    var contentStudioHtml = await contentStudio.Content.ReadAsStringAsync();
    Assert.Contains("OpenPortalKit", contentStudioHtml);
    Assert.Contains("Pages &amp; Templates", contentStudioHtml);
    Assert.Contains("aria-current=\"page\"", contentStudioHtml);
    Assert.Contains("Version history", contentStudioHtml);
    Assert.Contains("Revision 1", contentStudioHtml);
    Assert.False(contentStudioHtml.Contains("href=\"#workflow\"", StringComparison.Ordinal),
        "Admin navigation still exposes a non-functional workflow anchor.");

    using var formClient = CreateClient(host.BaseUri);
    using var templatesRequest = new HttpRequestMessage(HttpMethod.Get, "Templates");
    templatesRequest.Headers.TryAddWithoutValidation("Cookie", authCookie.Split(';', 2)[0]);
    using var templatesResponse = await formClient.SendAsync(templatesRequest);
    Assert.Equal(HttpStatusCode.OK, templatesResponse.StatusCode);
    var templatesHtml = await templatesResponse.Content.ReadAsStringAsync();
    var templateToken = ExtractAntiforgeryToken(templatesHtml);
    var antiforgeryCookie = templatesResponse.Headers.GetValues("Set-Cookie")
        .Select(value => value.Split(';', 2)[0])
        .Single(value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
    var adminCookies = authCookie.Split(';', 2)[0] + "; " + antiforgeryCookie;

    using var seedRequest = AuthenticatedPost(
        "Templates?handler=SeedTemplates",
        adminCookies,
        new Dictionary<string, string> { ["__RequestVerificationToken"] = templateToken });
    using var seedResponse = await formClient.SendAsync(seedRequest);
    Assert.Equal(HttpStatusCode.Redirect, seedResponse.StatusCode);

    using var createPageRequest = AuthenticatedPost(
        "Templates?handler=CreatePage",
        adminCookies,
        new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = templateToken,
            ["NewPage.TemplateCode"] = "corporate-homepage",
            ["NewPage.Title"] = "Integration structured page",
            ["NewPage.Slug"] = "integration-structured-page",
            ["NewPage.Summary"] = "A structured editor integration page."
        });
    using var createPageResponse = await formClient.SendAsync(createPageRequest);
    Assert.Equal(HttpStatusCode.Redirect, createPageResponse.StatusCode);

    using var editorRequest = new HttpRequestMessage(
        HttpMethod.Get, "Templates/PageEdit?slug=integration-structured-page");
    editorRequest.Headers.TryAddWithoutValidation("Cookie", authCookie.Split(';', 2)[0]);
    using var editorResponse = await formClient.SendAsync(editorRequest);
    Assert.Equal(HttpStatusCode.OK, editorResponse.StatusCode);
    var editorHtml = await editorResponse.Content.ReadAsStringAsync();
    Assert.Contains("Save revision", editorHtml);
    Assert.Contains("Expert JSON", editorHtml);
    Assert.Contains("Headline", editorHtml);
    Assert.Contains("Action URL", editorHtml);
    Assert.Contains("name=\"Editor.Revision\"", editorHtml);

    using var draftRequest = new HttpRequestMessage(HttpMethod.Get, "Content?status=Draft");
    draftRequest.Headers.TryAddWithoutValidation("Cookie", authCookie.Split(';', 2)[0]);
    using var draftResponse = await client.SendAsync(draftRequest);
    Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
    var draftHtml = await draftResponse.Content.ReadAsStringAsync();
    Assert.Contains("Structured publishing guide", draftHtml);
    Assert.False(draftHtml.Contains("Service availability announcement", StringComparison.Ordinal),
        "Admin status filter returned content from another state.");
    Assert.False(draftHtml.Contains("not valid for PageNumber", StringComparison.Ordinal),
        "Admin pagination query conflicts with the Razor page route value.");

    using var emptyRequest = new HttpRequestMessage(HttpMethod.Get, "Content?q=no-such-content");
    emptyRequest.Headers.TryAddWithoutValidation("Cookie", authCookie.Split(';', 2)[0]);
    using var emptyResponse = await client.SendAsync(emptyRequest);
    Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
    Assert.Contains("No content matches the current filters.", await emptyResponse.Content.ReadAsStringAsync());
}

static async Task<(string Token, string Html)> GetLoginPageAsync(HttpClient client, string returnUrl)
{
    using var response = await client.GetAsync("Account/Login?ReturnUrl=" + Uri.EscapeDataString(returnUrl));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var html = await response.Content.ReadAsStringAsync();
    var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
    Assert.True(match.Success, "Login form omitted the antiforgery token.");
    return (WebUtility.HtmlDecode(match.Groups[1].Value), html);
}

static FormUrlEncodedContent Form(string userName, string password, string token, string returnUrl)
{
    var fields = new Dictionary<string, string>
    {
        ["Input.UserName"] = userName,
        ["Input.Password"] = password,
        ["ReturnUrl"] = returnUrl
    };
    if (!string.IsNullOrWhiteSpace(token)) fields["__RequestVerificationToken"] = token;
    return new FormUrlEncodedContent(fields);
}

static string ExtractAntiforgeryToken(string html)
{
    var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
    Assert.True(match.Success, "Authenticated form omitted the antiforgery token.");
    return WebUtility.HtmlDecode(match.Groups[1].Value);
}

static HttpRequestMessage AuthenticatedPost(
    string path,
    string cookies,
    IReadOnlyDictionary<string, string> fields)
{
    var request = new HttpRequestMessage(HttpMethod.Post, path)
    {
        Content = new FormUrlEncodedContent(fields)
    };
    request.Headers.TryAddWithoutValidation("Cookie", cookies);
    return request;
}

static bool HasAuthenticationCookie(HttpResponseMessage response) =>
    response.Headers.TryGetValues("Set-Cookie", out var values) &&
    values.Any(value => value.StartsWith("__Host-OpenPortalKit.Admin=", StringComparison.Ordinal));

static HttpClient CreateClient(Uri baseUri, CookieContainer? cookies = null)
{
    var handler = new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        CookieContainer = cookies ?? new CookieContainer(),
        UseCookies = true
    };
    return new HttpClient(handler)
    {
        BaseAddress = baseUri,
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = 2 * 1024 * 1024
    };
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "OpenPortalKit.sln"))) return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}

file sealed class TestHost : IAsyncDisposable
{
    private readonly Process _process;
    private readonly List<string> _output = [];
    private readonly object _outputLock = new();

    private TestHost(Process process, Uri baseUri)
    {
        _process = process;
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public static async Task<TestHost> StartAsync(
        string repositoryRoot,
        string projectName,
        IReadOnlyDictionary<string, string?> environment)
    {
        var port = ReservePort();
        var projectDirectory = Path.Combine(repositoryRoot, "src", projectName);
        var hostAssembly = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", projectName + ".dll");
        if (!File.Exists(hostAssembly))
            throw new FileNotFoundException("Build the solution before running host integration tests.", hostAssembly);

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(hostAssembly);
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["AllowedHosts"] = "127.0.0.1;localhost";
        foreach (var item in environment) startInfo.Environment[item.Key] = item.Value;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var host = new TestHost(process, new Uri($"http://127.0.0.1:{port}/"));
        process.OutputDataReceived += (_, args) => host.Capture(args.Data);
        process.ErrorDataReceived += (_, args) => host.Capture(args.Data);
        if (!process.Start()) throw new InvalidOperationException($"Could not start {projectName}.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await host.WaitUntilReadyAsync();
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process.Dispose();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(2)
        };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        HttpStatusCode? lastStatusCode = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"Host exited with code {_process.ExitCode}.\n{Output()}");
            try
            {
                using var response = await client.GetAsync("health/live");
                lastStatusCode = response.StatusCode;
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            await Task.Delay(100);
        }
        var status = lastStatusCode is null ? "no HTTP response" : $"last status {(int)lastStatusCode} {lastStatusCode}";
        throw new TimeoutException($"Host did not become live ({status}).\n" + Output());
    }

    private void Capture(string? line)
    {
        if (line is null) return;
        lock (_outputLock)
        {
            if (_output.Count < 200) _output.Add(line);
        }
    }

    private string Output()
    {
        lock (_outputLock) return string.Join(Environment.NewLine, _output);
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

file static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected text to contain '{expected}'.");
    }

    public static void Header(HttpResponseMessage response, string name, string expected)
    {
        True(response.Headers.TryGetValues(name, out var values) && values.Contains(expected, StringComparer.Ordinal),
            $"Response omitted {name}: {expected}.");
    }

    public static void MediaType(string expected, string? actual) => Equal(expected, actual);

    public static void SecurityHeaders(HttpResponseMessage response)
    {
        True(response.Headers.Contains("Content-Security-Policy"), "CSP header is missing.");
        Header(response, "X-Content-Type-Options", "nosniff");
        Header(response, "X-Frame-Options", "DENY");
        True(response.Headers.Contains("X-Trace-Id"), "Trace header is missing.");
    }
}
