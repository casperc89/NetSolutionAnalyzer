using System.Web;

namespace RefactorCli.SampleLegacyWeb;

public sealed class AmbientContextUsage
{
    public void TouchAmbientContext()
    {
        _ = HttpContext.Current;
        _ = HttpContext.Current.Request;
        _ = HttpContext.Current.Response;
        _ = HttpContext.Current.User;
        HttpContext.Current.Items["CorrelationId"] = "sample-correlation-id";
    }
}
