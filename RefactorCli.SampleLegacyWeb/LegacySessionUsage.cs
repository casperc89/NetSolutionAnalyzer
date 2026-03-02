using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacySessionController : Controller
{
    public void HandleSession()
    {
        var dynamicKey = SessionLegacyConstants.SessionPrefix + DateTime.UtcNow.Second;

        Session["ControllerUserId"] = "42";
        _ = Session["ControllerUserId"];
        Session[dynamicKey] = "dynamic-write";
        _ = Session[dynamicKey];
        _ = Session.GetString("ControllerUserId");

        System.Web.HttpContext.Current.Session["CurrentUserId"] = "42";
        _ = System.Web.HttpContext.Current.Session["CurrentUserId"];

        HttpSessionState session = System.Web.HttpContext.Current.Session;
        _ = session[SessionLegacyConstants.UiLanguage];

        HttpContext.Session[SessionLegacyConstants.UiLanguage] = "fi";
        _ = HttpContext.Session[SessionLegacyConstants.UiLanguage];
        HttpContext.Session["Moduulit"] = new object();

        SessionStateItemCollection? items = null;
        _ = items;
    }
}

public static class SessionLegacyExtensions
{
    public static string? GetString(this HttpSessionStateBase session, string key)
    {
        return session[key] as string;
    }
}

internal static class SessionLegacyConstants
{
    public const string UiLanguage = "KAYTTOLIITTYMA_KIELI";
    public const string SessionPrefix = "ONMINIKILPAILUTUS_";
}
