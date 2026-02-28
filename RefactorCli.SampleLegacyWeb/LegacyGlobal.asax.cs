using System.Web;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacyGlobal : HttpApplication
{
    public void Application_BeginRequest()
    {
        _ = HttpContext.Current.Server.MapPath("~/content");
    }

    public void Application_EndRequest()
    {
    }

    public void Application_Error()
    {
    }
}
