namespace System.Web;

public class HttpApplication
{
    public HttpServerUtility Server { get; } = new();
}

public class HttpContext
{
    public static HttpContext Current { get; set; } = new();

    public string RequestPath { get; set; } = "/";

    public HttpSessionState Session { get; } = new();

    public HttpServerUtility Server { get; } = new();

    public HttpRequest Request { get; } = new();

    public HttpResponse Response { get; } = new();

    public object User { get; set; } = new();

    public Dictionary<string, object?> Items { get; } = new(StringComparer.Ordinal);
}

public class HttpContextBase
{
}

public interface IHttpHandler
{
    void ProcessRequest(HttpContext context);
}

public sealed class HttpServerUtility
{
    public string MapPath(string virtualPath)
    {
        return $"/legacy-root/{virtualPath.TrimStart('~', '/')}";
    }
}

public sealed class HttpRequest
{
}

public sealed class HttpResponse
{
}

public sealed class HttpSessionState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public object? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value;
    }
}
