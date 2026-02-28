namespace System.Web
{
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
        public HttpFileCollectionBase Files { get; } = new();

        public HttpCookieCollection Cookies { get; } = new();
    }

    public sealed class HttpResponse
    {
        public HttpCookieCollection Cookies { get; } = new();
    }

    public class HttpPostedFileBase
    {
        public Stream InputStream => Stream.Null;
    }

    public sealed class HttpFileCollectionBase
    {
        private readonly List<HttpPostedFileBase> _files = [];

        public HttpPostedFileBase? this[int index]
        {
            get => index >= 0 && index < _files.Count ? _files[index] : null;
        }

        public void Add(HttpPostedFileBase file)
        {
            _files.Add(file);
        }
    }

    public sealed class HttpCookie
    {
        public HttpCookie(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; set; }
    }

    public sealed class HttpCookieCollection
    {
        private readonly Dictionary<string, HttpCookie> _cookies = new(StringComparer.Ordinal);

        public HttpCookie? this[string key]
        {
            get => _cookies.TryGetValue(key, out var cookie) ? cookie : null;
            set
            {
                if (value is not null)
                {
                    _cookies[key] = value;
                }
            }
        }

        public void Add(HttpCookie cookie)
        {
            _cookies[cookie.Name] = cookie;
        }
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
}

namespace System.Web.Mvc
{
    public abstract class Controller
    {
        public System.Web.HttpSessionState Session => System.Web.HttpContext.Current.Session;
    }
}

namespace System.Web.SessionState
{
    public sealed class SessionStateItemCollection
    {
    }
}
