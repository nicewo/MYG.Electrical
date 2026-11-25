using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace MYG.Electrical.Drawing.Tests;

/// <summary>
/// Themes/Brand.xaml içindeki vektör yollar derlemede değil çalışma anında çözümlenir;
/// hatalı bir yol Hakkında penceresini çökertir. Bu test dosyayı gerçekten yükler.
/// </summary>
public class BrandResourcesTests
{
    private static string BrandXamlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MYG.Electrical.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "src", "MYG.Electrical.App", "Themes", "Brand.xaml");
    }

    [Fact]
    public void Myg_tech_logo_parses_and_has_wordmark_proportions() => Sta.Run(() =>
    {
        var dictionary = (ResourceDictionary)XamlReader.Parse(File.ReadAllText(BrandXamlPath()));

        var logo = Assert.IsType<DrawingImage>(dictionary["MygTechLogo"]);
        var bounds = logo.Drawing.Bounds;
        Assert.InRange(bounds.Width, 1000, 1040);   // SVG viewBox 1027 × 305
        Assert.InRange(bounds.Height, 280, 320);
        Assert.NotNull(dictionary["LinkButton"]);
    });
}
