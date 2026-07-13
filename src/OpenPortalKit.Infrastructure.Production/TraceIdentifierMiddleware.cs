using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OpenPortalKit.Infrastructure.Production;

public sealed partial class TraceIdentifierMiddleware
{
    public const string HeaderName = "X-Trace-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<TraceIdentifierMiddleware> _logger;

    public TraceIdentifierMiddleware(RequestDelegate next, ILogger<TraceIdentifierMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = ResolveTraceId(context);
        context.TraceIdentifier = traceId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = traceId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId }))
        {
            await _next(context);
        }
    }

    public static string ResolveTraceId(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(supplied) && ValidTraceId().IsMatch(supplied))
        {
            return supplied;
        }

        return Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{7,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidTraceId();
}
