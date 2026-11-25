namespace MYG.Electrical.Drawing;

/// <summary>Çizim alanındaki aktif araç.</summary>
public enum EditorTool
{
    Select,
    Line,
    Rectangle,
    Text,
    Symbol,

    /// <summary>Dirsek (90° dönüş) bağlantı noktası.</summary>
    Angle,

    /// <summary>T bağlantı noktası (3 yön).</summary>
    Tee,

    /// <summary>Çapraz düğüm (4 yön) — yalnızca bilerek seçildiğinde.</summary>
    Cross,

    /// <summary>Hazırlanmış resmi tıklanan noktaya (sol üst köşe) yerleştirir.</summary>
    Image,

    /// <summary>Potansiyel tanımı (L1, +24V...).</summary>
    Potential,

    /// <summary>Sayfalar arası bağlantı (kesme noktası).</summary>
    PageLink,
}

public static class EditorToolExtensions
{
    /// <summary>Tıklayınca nesne yerleştiren (önizlemesi olan) araçlar.</summary>
    public static bool IsPlacementTool(this EditorTool tool) =>
        tool is EditorTool.Symbol or EditorTool.Angle or EditorTool.Tee or EditorTool.Cross or EditorTool.Image
            or EditorTool.Potential or EditorTool.PageLink;

    public static Core.Model.JunctionShape? ToJunctionShape(this EditorTool tool) => tool switch
    {
        EditorTool.Angle => Core.Model.JunctionShape.Angle,
        EditorTool.Tee => Core.Model.JunctionShape.Tee,
        EditorTool.Cross => Core.Model.JunctionShape.Cross,
        _ => null,
    };
}
