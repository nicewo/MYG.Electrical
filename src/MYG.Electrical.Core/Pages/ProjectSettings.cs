namespace MYG.Electrical.Core.Pages;

/// <summary>Projeye özel ayarlar (numaralandırma şablonları).</summary>
public sealed class ProjectSettings
{
    public const string DefaultReferenceFormat = "-{prefix}{n}";
    public const string DefaultWireNumberFormat = "{n}";

    /// <summary>Cihaz etiketi şablonu. Yer tutucular: {prefix} (IEC 81346-2 harfi), {n} (sıra), {page} (sayfa no).</summary>
    public string ReferenceFormat { get; set; } = DefaultReferenceFormat;

    /// <summary>Kablo numarası şablonu. Yer tutucular: {n}, {page}, {column}.</summary>
    public string WireNumberFormat { get; set; } = DefaultWireNumberFormat;
}
