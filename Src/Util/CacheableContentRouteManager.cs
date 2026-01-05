using WatsonWebserver.Core;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace LAHEE.Util;

public class CacheableContentRouteManager : ContentRouteManager {
    private int CacheSeconds { get; }

    private readonly List<string> defaultFiles = new List<string> {
        "index.html",
        "index.html",
        "default.html",
        "default.htm",
        "home.html",
        "home.htm",
        "home.cgi",
        "welcome.html",
        "welcome.htm",
        "index.php",
        "default.aspx",
        "default.asp"
    };

    public CacheableContentRouteManager(int cacheSeconds) {
        CacheSeconds = cacheSeconds > 0 ? cacheSeconds : 604800;
        Handler = InternalHandler;
    }

    private async Task InternalHandler(HttpContextBase ctx) {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
        if (ctx.Response == null) throw new ArgumentNullException(nameof(ctx.Response));

        string baseDirectory = BaseDirectory;
        baseDirectory = baseDirectory.Replace("\\", "/");
        if (!baseDirectory.EndsWith("/")) baseDirectory += "/";

        if (ctx.Request.Method != HttpMethod.GET
            && ctx.Request.Method != HttpMethod.HEAD) {
            Set500Response(ctx);
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
            return;
        }

        string filePath = ctx.Request.Url.RawWithoutQuery;
        if (!String.IsNullOrEmpty(filePath)) {
            while (filePath.StartsWith("/")) filePath = filePath.Substring(1);
        }

        bool isDirectory =
            filePath.EndsWith("/")
            || String.IsNullOrEmpty(filePath)
            || Directory.Exists(baseDirectory + filePath);

        if (isDirectory && !filePath.EndsWith("/")) filePath += "/";

        filePath = baseDirectory + filePath;
        filePath = filePath.Replace("+", " ").Replace("%20", " ");

        if (isDirectory && defaultFiles.Count > 0) {
            foreach (string defaultFile in defaultFiles) {
                if (File.Exists(filePath + defaultFile)) {
                    filePath += defaultFile;
                    break;
                }
            }
        }

        if (!File.Exists(filePath)) {
            Set404Response(ctx);
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
            return;
        }

        FileInfo fi = new FileInfo(filePath);
        long contentLength = fi.Length;

        if (ctx.Request.Method == HttpMethod.GET) {
            FileStream fs = new FileStream(filePath, ContentFileMode, ContentFileAccess, ContentFileShare);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength = contentLength;
            ctx.Response.ContentType = GetContentType(filePath);

            if (ctx.Response.ContentType.StartsWith("image/")) {
                ctx.Response.Headers.Add("Cache-Control", "max-age=" + CacheSeconds);
            }

            await ctx.Response.Send(contentLength, fs, ctx.Token).ConfigureAwait(false);
        } else if (ctx.Request.Method == HttpMethod.HEAD) {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength = contentLength;
            ctx.Response.ContentType = GetContentType(filePath);
            await ctx.Response.Send(contentLength, ctx.Token).ConfigureAwait(false);
        } else {
            Set500Response(ctx);
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
        }
    }

    private string GetContentType(string path) {
        if (String.IsNullOrEmpty(path)) return "application/octet-stream";

        int idx = path.LastIndexOf(".", StringComparison.Ordinal);
        if (idx >= 0) {
            return MimeTypes.GetFromExtension(path.Substring(idx));
        }

        return "application/octet-stream";
    }

    private void Set404Response(HttpContextBase ctx) {
        ctx.Response.StatusCode = 404;
        ctx.Response.ContentLength = 0;
    }

    private void Set500Response(HttpContextBase ctx) {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentLength = 0;
    }
}