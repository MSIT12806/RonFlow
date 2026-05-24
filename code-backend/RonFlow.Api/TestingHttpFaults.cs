using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace RonFlow.Api;

public sealed record TestHttpFault(string Method, string PathPattern, int? StatusCode, int DelayMs, string Message);

public interface ITestHttpFaultStore
{
    void Replace(Guid userId, IReadOnlyList<TestHttpFault> faults);

    void Clear(Guid userId);

    TestHttpFault? Match(Guid userId, string method, string path);
}

internal sealed class InMemoryTestHttpFaultStore : ITestHttpFaultStore
{
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<TestHttpFault>> faultsByUserId = new();

    public void Replace(Guid userId, IReadOnlyList<TestHttpFault> faults)
    {
        faultsByUserId[userId] = faults;
    }

    public void Clear(Guid userId)
    {
        faultsByUserId.TryRemove(userId, out _);
    }

    public TestHttpFault? Match(Guid userId, string method, string path)
    {
        if (!faultsByUserId.TryGetValue(userId, out var faults))
        {
            return null;
        }

        return faults.FirstOrDefault(fault =>
            string.Equals(fault.Method, method, StringComparison.OrdinalIgnoreCase)
            && PathMatches(fault.PathPattern, path));
    }

    private static bool PathMatches(string pattern, string path)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "[^/]+") + "$";
        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

internal sealed class NoOpTestHttpFaultStore : ITestHttpFaultStore
{
    public void Replace(Guid userId, IReadOnlyList<TestHttpFault> faults)
    {
    }

    public void Clear(Guid userId)
    {
    }

    public TestHttpFault? Match(Guid userId, string method, string path)
    {
        return null;
    }
}

internal sealed class TestHttpFaultMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITestHttpFaultStore faultStore)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (Guid.TryParse(rawUserId, out var userId))
            {
                var fault = faultStore.Match(userId, context.Request.Method, context.Request.Path.Value ?? string.Empty);
                if (fault is not null)
                {
                    if (fault.DelayMs > 0)
                    {
                        await Task.Delay(fault.DelayMs, context.RequestAborted);
                    }

                    if (fault.StatusCode is not null)
                    {
                        context.Response.StatusCode = fault.StatusCode.Value;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { message = fault.Message }, cancellationToken: context.RequestAborted);
                        return;
                    }
                }
            }
        }

        await next(context);
    }
}

public sealed record ReplaceTestHttpFaultsRequest(IReadOnlyList<TestHttpFaultRequest> Items);

public sealed record TestHttpFaultRequest(string Method, string PathPattern, int? StatusCode, int DelayMs = 0, string? Message = null)
{
    public TestHttpFault ToFault()
    {
        return new TestHttpFault(Method, PathPattern, StatusCode, DelayMs, Message ?? "Fault injected");
    }
}