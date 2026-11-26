using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Symbols;

/// <summary>Yerleşik (genel) cihaz kütüphanesi: gömülü <c>Devices/*.json</c>, <c>tools/symbolgen/devices.py</c> ile üretilir.</summary>
public static class BuiltInDeviceLibrary
{
    private const string ResourcePrefix = "MYG.Electrical.Symbols.Devices.";

    /// <summary>Yerleşik cihazlar (katalogdaki sembollere göre doğrulanmış).</summary>
    public static DeviceLibrary Load(ISymbolCatalog catalog) => Create(catalog, null);

    /// <summary>Yerleşik + kullanıcı klasöründeki cihazlar.</summary>
    public static DeviceLibrary Create(ISymbolCatalog catalog, string? userDirectory)
    {
        var warnings = new List<string>();
        var templates = new List<DeviceTemplate>();
        var asm = typeof(BuiltInDeviceLibrary).Assembly;
        foreach (var name in asm.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            templates.AddRange(DeviceLibraryLoader.Read(stream, name[ResourcePrefix.Length..], warnings, catalog));
        }
        if (userDirectory is not null)
        {
            templates.AddRange(DeviceLibraryLoader.ReadDirectory(userDirectory, warnings, catalog));
        }
        return new DeviceLibrary(templates, warnings);
    }
}
