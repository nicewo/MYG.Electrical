namespace MYG.Electrical.Core.Rendering;

public sealed class SceneComposerOptions
{
    /// <summary>Bağlantı noktası işaretleri (yalnızca editörde).</summary>
    public bool ShowConnectionPoints { get; init; } = true;

    /// <summary>Pin numaraları (A1, 13, 14...) — çıktıda da basılır.</summary>
    public bool ShowPinLabels { get; init; } = true;

    /// <summary>Gizli ve boş bilgi alanları soluk gösterilir (yalnızca editörde).</summary>
    public bool ShowHiddenFields { get; init; } = true;

    public static readonly SceneComposerOptions Editor = new();
    public static readonly SceneComposerOptions Print = new() { ShowConnectionPoints = false, ShowHiddenFields = false };
}
