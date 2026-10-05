using System.Globalization;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Jobby.Dashboard;

internal static class JobbyDashboardShell
{
    // Note: per-build token for cache validators. Stable inside one deployed binary.
    private static readonly string AssetVersionToken =
        typeof(JobbyDashboardShell).Assembly.ManifestModule.ModuleVersionId.ToString("N");

    internal static async Task ServeAsync(HttpContext ctx, string prefix, string path = "")
    {
        if (path.Equals("api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (path.Length > 0 && !path.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            var asset = JobbyDashboardClientAssets.Files.GetFileInfo(path);
            if (asset is { Exists: true, IsDirectory: false })
            {
                await ServeAssetAsync(ctx, path, rawAsset: asset);
                return;
            }

            var brotliAsset = JobbyDashboardClientAssets.Files.GetFileInfo(path + ".br");
            if (brotliAsset is { Exists: true, IsDirectory: false })
            {
                await ServeAssetAsync(ctx, path, rawAsset: null, brotliAsset);
                return;
            }
        }

        await WriteIndexAsync(ctx, prefix);
    }

    private static async Task ServeAssetAsync(HttpContext ctx, string path, IFileInfo? rawAsset,
        IFileInfo? brotliAsset = null)
    {
        // Note: .NET 8 WASM assets here are not fingerprinted. Revalidate to keep boot integrity hashes coherent.
        var etag = $"W/\"{AssetVersionToken}-{(rawAsset ?? brotliAsset)!.Length:x}\"";
        ctx.Response.Headers.ETag = etag;
        ctx.Response.Headers.Vary = "Accept-Encoding";
        ctx.Response.Headers.CacheControl = "no-cache";

        if (IfNoneMatch(ctx.Request, etag))
        {
            ctx.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        ctx.Response.ContentType = JobbyDashboardClientAssets.ContentTypes.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";

        brotliAsset ??= JobbyDashboardClientAssets.Files.GetFileInfo(path + ".br") is { Exists: true, IsDirectory: false } found
            ? found
            : null;

        if (AcceptsBrotli(ctx.Request.Headers.AcceptEncoding) &&
            brotliAsset is not null)
        {
            ctx.Response.Headers.ContentEncoding = "br";
            ctx.Response.ContentLength = brotliAsset.Length;
            await using var compressed = brotliAsset.CreateReadStream();
            await compressed.CopyToAsync(ctx.Response.Body);
            return;
        }

        if (rawAsset is not null)
        {
            ctx.Response.ContentLength = rawAsset.Length;
            await using var stream = rawAsset.CreateReadStream();
            await stream.CopyToAsync(ctx.Response.Body);
            return;
        }

        await using var compressedFallback = brotliAsset!.CreateReadStream();
        await using var decompressed = new BrotliStream(compressedFallback, CompressionMode.Decompress);
        await decompressed.CopyToAsync(ctx.Response.Body);
    }

    private static bool IfNoneMatch(HttpRequest request, string etag)
    {
        foreach (var value in request.Headers.IfNoneMatch)
        {
            if (value is null)
            {
                continue;
            }

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var candidate in value.Split(','))
            {
                var tag = candidate.Trim();
                if (tag == "*" || tag == etag)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AcceptsBrotli(string? acceptEncoding)
    {
        if (string.IsNullOrWhiteSpace(acceptEncoding))
        {
            return false;
        }

        foreach (var part in acceptEncoding.Split(','))
        {
            var token = part.Trim();
            var quality = 1.0;
            var semicolon = token.IndexOf(';');
            if (semicolon >= 0)
            {
                var parameters = token[(semicolon + 1)..].Trim();
                token = token[..semicolon].Trim();
                if (parameters.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(parameters[2..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var q))
                {
                    quality = q;
                }
            }

            if (token.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                return quality > 0;
            }
        }

        return false;
    }

    private static async Task WriteIndexAsync(HttpContext ctx, string prefix)
    {
        var file = JobbyDashboardClientAssets.Files.GetFileInfo("index.html");
        if (!file.Exists)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsync("Jobby Dashboard client assets are not embedded.");
            return;
        }

        string html;
        await using (var stream = file.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            html = await reader.ReadToEndAsync();
        }

        var pathBase = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value : string.Empty;
        var baseHref = pathBase + prefix + "/";
        html = html
            .Replace("<base href=\"/\"/>", $"<base href=\"{baseHref}\" />")
            .Replace("<base href=\"/\" />", $"<base href=\"{baseHref}\" />");

        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(html);
    }
}
