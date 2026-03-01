using System.Web;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacyCookieUsage
{
    public void HandleCookies()
    {
        var dynamicKey = "theme-" + DateTime.UtcNow.Second;
        var cookie = new HttpCookie("theme", "dark");
        HttpContext.Current.Response.Cookies.Add(cookie);
        HttpContext.Current.Response.Cookies["theme"] = cookie;
        HttpContext.Current.Response.Cookies[dynamicKey] = cookie;
        _ = HttpContext.Current.Request.Cookies["theme"];
        _ = HttpContext.Current.Request.Cookies[dynamicKey];
    }
}
