using System.Web;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacyHandler : IHttpHandler
{
    private readonly System.Web.HttpContext _context = new();

    public void ProcessRequest(HttpContext context)
    {
        _ = _context.RequestPath;
        _ = context.RequestPath;

        HttpContext.Current = context;
        HttpContext.Current.Session["CurrentUserId"] = "42";

        var currentContext = HttpContext.Current;
        var userId = currentContext.Session["CurrentUserId"];
        _ = userId;
    }
}
