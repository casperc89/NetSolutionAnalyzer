using System.Web;

namespace RefactorCli.SampleLegacyWeb;

public sealed class LegacyFileUploadUsage
{
    public void HandleUpload(HttpPostedFileBase postedFile)
    {
        _ = postedFile.InputStream;

        HttpContext.Current.Request.Files.Add(postedFile);
        _ = HttpContext.Current.Request.Files[0];
    }
}
