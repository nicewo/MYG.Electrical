using System.Globalization;
using System.Reflection;
using System.Resources;
using MYG.Electrical.App.Localization;

namespace MYG.Electrical.Drawing.Tests;

/// <summary>Panel menülerinin Türkçeleştirilmesi: AvalonDock iç yapısı değişirse bu test kırılır.</summary>
public class DockingLocalizationTests
{
    private static Type ResourcesType =>
        typeof(AvalonDock.DockingManager).Assembly.GetType("AvalonDock.Properties.Resources", throwOnError: true)!;

    private static string? Text(string key) =>
        ResourcesType.GetProperty(key, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;

    [Fact]
    public void Turkish_texts_replace_library_strings()
    {
        Assert.True(DockingLocalization.Apply("tr"));

        Assert.Equal("Kayan", Text("Anchorable_Float"));
        Assert.Equal("Otomatik gizle", Text("Anchorable_AutoHide"));
        Assert.Equal("Sekmeli belge olarak yerleştir", Text("Anchorable_DockAsDocument"));
    }

    [Fact]
    public void Every_translated_key_exists_in_library()
    {
        DockingLocalization.Apply("tr");
        var manager = (ResourceManager)ResourcesType
            .GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var original = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!;
        foreach (var key in DockingLocalization.Languages["tr"].Keys)
        {
            Assert.False(string.IsNullOrEmpty(original.GetString(key)), $"AvalonDock'ta '{key}' anahtarı yok");
        }
    }

    [Fact]
    public void Unknown_language_keeps_original_texts() => Assert.False(DockingLocalization.Apply("xx"));
}
