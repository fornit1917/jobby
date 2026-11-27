using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Jobby.Dashboard;

internal static class JobbyDashboardClientAssets
{
    public static readonly IFileProvider Files =
        new ManifestEmbeddedFileProvider(typeof(JobbyDashboardClientAssets).Assembly, "wwwroot");

    public static readonly IContentTypeProvider ContentTypes = Build();

    private static FileExtensionContentTypeProvider Build()
    {
        var p = new FileExtensionContentTypeProvider
        {
            Mappings =
            {
                [".wasm"] = "application/wasm",
                [".dll"] = "application/octet-stream",
                [".pdb"] = "application/octet-stream",
                [".dat"] = "application/octet-stream",
                [".blat"] = "application/octet-stream",
                [".webcil"] = "application/octet-stream",
                [".br"] = "application/octet-stream",
                [".gz"] = "application/octet-stream"
            }
        };
        p.Mappings.TryAdd(".json", "application/json");
        p.Mappings.TryAdd(".webmanifest", "application/manifest+json");
        return p;
    }
}
