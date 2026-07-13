using Microsoft.AspNetCore.Http;
using OpenPortalKit.Infrastructure.Production;
using Microsoft.Extensions.Configuration;

var tests = new (string Name, Action Run)[]
{
    ("security policy emits browser hardening headers", SecurityPolicyIsComplete),
    ("trace middleware accepts bounded safe identifiers", TraceIdentifierAcceptsSafeInput),
    ("trace middleware rejects unsafe identifiers", TraceIdentifierRejectsUnsafeInput),
    ("production limits are enabled by default", ProductionDefaultsAreDefensive),
    ("redis endpoint parsing is bounded", RedisEndpointParsingIsBounded),
    ("production host configuration fails closed", ProductionHostConfigurationFailsClosed)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

static void SecurityPolicyIsComplete()
{
    var required = new[]
    {
        "Content-Security-Policy",
        "Cross-Origin-Opener-Policy",
        "Permissions-Policy",
        "Referrer-Policy",
        "X-Content-Type-Options",
        "X-Frame-Options"
    };
    Assert(required.All(SecurityHeaderPolicy.Headers.ContainsKey), "Security header policy is incomplete.");
    Assert(SecurityHeaderPolicy.Headers["Content-Security-Policy"].Contains("frame-ancestors 'none'", StringComparison.Ordinal),
        "CSP must prevent framing.");
    Assert(!SecurityHeaderPolicy.Headers["Content-Security-Policy"].Contains("script-src 'self' 'unsafe-inline'", StringComparison.Ordinal),
        "CSP must not allow arbitrary inline scripts.");
}

static void TraceIdentifierAcceptsSafeInput()
{
    var context = new DefaultHttpContext();
    context.Request.Headers[TraceIdentifierMiddleware.HeaderName] = "deploy-20260713:request_42";
    Assert(TraceIdentifierMiddleware.ResolveTraceId(context) == "deploy-20260713:request_42",
        "Valid caller trace ID was not preserved.");
}

static void TraceIdentifierRejectsUnsafeInput()
{
    var context = new DefaultHttpContext();
    context.Request.Headers[TraceIdentifierMiddleware.HeaderName] = "bad trace\r\nInjected: true";
    var resolved = TraceIdentifierMiddleware.ResolveTraceId(context);
    Assert(resolved != "bad trace\r\nInjected: true", "Unsafe trace ID was trusted.");
    Assert(resolved.Length == 32, "Generated trace ID must use W3C trace width.");
}

static void ProductionDefaultsAreDefensive()
{
    var options = new ProductionHardeningOptions();
    Assert(options.EnableHttpsRedirection, "HTTPS redirection must default on.");
    Assert(options.EnableHsts, "HSTS must default on.");
    Assert(options.EnableRateLimiting, "Rate limiting must default on.");
    Assert(options.AdminRequestsPerMinute < options.PublicRequestsPerMinute,
        "Admin rate limit must be stricter than the public limit.");
    Assert(options.LoginAttemptsPerFiveMinutes <= 10,
        "Login endpoint must use a low request budget.");
}

static void RedisEndpointParsingIsBounded()
{
    Assert(RedisReadinessHealthCheck.TryParseEndpoint("redis.internal:6380,abortConnect=false", out var host, out var port),
        "Redis endpoint was not parsed.");
    Assert(host == "redis.internal" && port == 6380, "Redis host or port was parsed incorrectly.");
    Assert(!RedisReadinessHealthCheck.TryParseEndpoint("", out _, out _), "Empty Redis endpoint was accepted.");
}

static void ProductionHostConfigurationFailsClosed()
{
    var wildcard = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["AllowedHosts"] = "*",
        ["PublicBaseUrl"] = "http://portal.example"
    }).Build();
    AssertThrows<InvalidOperationException>(() =>
        ProductionConfigurationValidator.ValidateWebHost(wildcard, isDevelopment: false));
    AssertThrows<InvalidOperationException>(() =>
        ProductionConfigurationValidator.ValidateHttpsEndpoint(wildcard, "PublicBaseUrl", isDevelopment: false));

    var production = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["AllowedHosts"] = "portal.example;admin.portal.example",
        ["PublicBaseUrl"] = "https://portal.example"
    }).Build();
    ProductionConfigurationValidator.ValidateWebHost(production, isDevelopment: false);
    ProductionConfigurationValidator.ValidateHttpsEndpoint(production, "PublicBaseUrl", isDevelopment: false);
    ProductionConfigurationValidator.ValidateWebHost(wildcard, isDevelopment: true);
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
