using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Editing;

public sealed class EditorSettings
{
    public double GridSpacing { get; set; } = GeometryConstants.DefaultGridSpacingMm;
    public bool GridVisible { get; set; } = true;
    public bool SnapEnabled { get; set; } = true;
    public bool ShowConnectionPoints { get; set; } = true;

    /// <summary>Yapıştırılan nesnelerin kaydırma miktarı (mm).</summary>
    public double PasteOffset { get; set; } = 2 * GeometryConstants.DefaultGridSpacingMm;
}
