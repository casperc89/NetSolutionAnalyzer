using System.Web;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacyCookieUsage
{
    public void HandleCookies()
    {
        var cookie = new HttpCookie("theme", "dark");
        HttpContext.Current.Response.Cookies.Add(cookie);
        _ = HttpContext.Current.Request.Cookies["theme"];
    }
}
