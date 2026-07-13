using Microsoft.AspNetCore.Http;
using OpenPortalKit.Infrastructure.Production;

var tests = new (string Name, Action Run)[]
{
    ("security policy emits browser hardening headers", SecurityPolicyIsComplete),
    ("trace middleware accepts bounded safe identifiers", TraceIdentifierAcceptsSafeInput),
    ("trace middleware rejects unsafe identifiers", TraceIdentifierRejectsUnsafeInput),
    ("production limits are enabled by default", ProductionDefaultsAreDefensive)
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

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
