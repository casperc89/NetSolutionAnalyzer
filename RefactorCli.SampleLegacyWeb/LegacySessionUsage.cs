using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacySessionController : Controller
{
    public void HandleSession()
    {
        var dynamicKey = "Controller_" + DateTime.UtcNow.Second;

        Session["ControllerUserId"] = "42";
        _ = Session["ControllerUserId"];
        Session[dynamicKey] = "dynamic-write";
        _ = Session[dynamicKey];
        _ = Session.GetString("ControllerUserId");

        HttpContext.Current.Session["CurrentUserId"] = "42";
        _ = HttpContext.Current.Session["CurrentUserId"];

        SessionStateItemCollection? items = null;
        _ = items;
    }
}

public static class SessionLegacyExtensions
{
    public static string? GetString(this HttpSessionState session, string key)
    {
        return session[key] as string;
    }
}
