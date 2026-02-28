namespace System.Web;

public class HttpApplication
{
}

public class HttpContext
{
    public static HttpContext Current { get; set; } = new();

    public string RequestPath { get; set; } = "/";

    public HttpSessionState Session { get; } = new();
}

public class HttpContextBase
{
}

public interface IHttpHandler
{
    void ProcessRequest(HttpContext context);
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
