using System.Xml.Linq;

namespace Jobby.Tests.Dashboard;

public class DashboardEmbeddedAssetsBuildTests
{
    [Fact]
    public void EmbeddedDashboardAssets_DefineNativeTargetPath_ForCrossPlatformManifestPaths()
    {
        var project = XDocument.Load(FindDashboardProjectFile());
        var embeddedResource = project
            .Descendants("Target")
            .Single(t => (string?)t.Attribute("Name") == "EmbedJobbyDashboardClient")
            .Descendants("EmbeddedResource")
            .Single(e => (string?)e.Attribute("Include") == "@(_JobbyClientAsset)");

        Assert.Equal(
            "wwwroot$([System.IO.Path]::DirectorySeparatorChar)%(_JobbyClientAsset.RecursiveDir)%(_JobbyClientAsset.Filename)%(_JobbyClientAsset.Extension)",
            (string?)embeddedResource.Attribute("TargetPath"));
        Assert.Null((string?)embeddedResource.Attribute("LogicalName"));
    }

    private static string FindDashboardProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "Jobby.Dashboard",
                "Jobby.Dashboard.csproj");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find src/Jobby.Dashboard/Jobby.Dashboard.csproj.");
    }
}
