using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Editing;

/// <summary>Nesnelerin global sınırları ve isabet testleri.</summary>
public static class ObjectBounds
{
    private const double SymbolFallbackHalfSize = 5.0;

    public static Rect2D Get(DrawingObject obj, ISymbolCatalog catalog) => obj switch
    {
        LineObject l => Rect2D.FromPoints(l.Start, l.End),
        RectangleObject r => r.Bounds,
        ImageObject i => i.Bounds,
        TextObject t => TextBounds(t),
        FieldObject f => FieldBounds(f, null),
        JunctionObject j => JunctionBounds(j),
        SinglePinObject c => ConnectorBounds(c),
        SymbolObject s => SymbolBounds(s, catalog),
        _ => Rect2D.Empty,
    };

    public static Rect2D SymbolBounds(SymbolObject s, ISymbolCatalog catalog)
    {
        var local = catalog.Find(s.SymbolId)?.LocalBounds
            ?? new Rect2D(-SymbolFallbackHalfSize, -SymbolFallbackHalfSize, 2 * SymbolFallbackHalfSize, 2 * SymbolFallbackHalfSize);
        return CoordinateTransform.TransformBounds(local, s.Position, s.Rotation, s.Scale);
    }

    /// <summary>Junction: noktanın kendisi + aktif yönlerdeki kısa gösterge çizgileri.</summary>
    public static Rect2D JunctionBounds(JunctionObject j)
    {
        var r = Math.Max(j.Size / 2, Rendering.DrawingStyles.JunctionStubLength / 2);
        return new Rect2D(j.X - r, j.Y - r, 2 * r, 2 * r);
    }

    /// <summary>Potansiyel / sayfa bağlantısı: işaret + ad metni (yaklaşık).</summary>
    public static Rect2D ConnectorBounds(SinglePinObject c)
    {
        var textWidth = Math.Max(2, c.Name.Length) * Rendering.ConnectorGeometry.NameHeight * GeometryConstants.AverageCharWidthRatio;
        var local = c switch
        {
            PotentialObject => new Rect2D(-Math.Max(2.5, textWidth / 2), -Rendering.ConnectorGeometry.PotentialHeight - Rendering.ConnectorGeometry.NameHeight - 1,
                Math.Max(5, textWidth), Rendering.ConnectorGeometry.PotentialHeight + Rendering.ConnectorGeometry.NameHeight + 1),
            DevicePinObject => new Rect2D(-1, -1, 2 + Math.Max(2, c.Name.Length) * Rendering.DrawingStyles.PinTextHeight * GeometryConstants.AverageCharWidthRatio + 1, DevicePinObject.LeadLength + 1),
            _ => new Rect2D(0, -2.5, Rendering.ConnectorGeometry.LinkArrowLength + 1.5 + textWidth + 8, 5),
        };
        // Metin döndürülmez; sınır yaklaşık olarak işaretle birlikte döner.
        return CoordinateTransform.TransformBounds(local, c.Anchor, c.Rotation);
    }

    /// <summary>Metin sınırı tahmini: (X,Y) hizalamaya göre sol / orta / sağ uç, dikeyde orta.</summary>
    public static Rect2D TextBounds(TextObject t)
    {
        var width = Math.Max(1, t.Text.Length) * t.Height * GeometryConstants.AverageCharWidthRatio;
        var local = new Rect2D(AlignedLeft(t.Align, width), -t.Height / 2, width, t.Height);
        return CoordinateTransform.TransformBounds(local, t.Anchor, t.Rotation);
    }

    /// <summary>
    /// Bilgi alanı sınırı. Metin uzunluğu proje bilinmiyorsa en az etiket adı kadar sayılır; logo alanı
    /// yüksekliği kadar kare bir kutudur.
    /// </summary>
    public static Rect2D FieldBounds(FieldObject f, Pages.ProjectDocument? doc)
    {
        if (f.IsLogo)
        {
            return new Rect2D(f.X + AlignedLeft(f.Align, f.Height), f.Y - f.Height / 2, f.Height, f.Height);
        }
        var value = doc is null ? string.Empty : Pages.TitleBlockFields.ResolveField(doc, null, f.Field);
        var text = doc is null && f.RenderedText is { Length: > 0 } rendered
            ? rendered
            : f.Compose(value.Length > 0 ? value : $"[{Pages.TitleBlockFields.LabelOf(f.Field)}]");
        var width = Math.Max(4, text.Length) * f.Height * GeometryConstants.AverageCharWidthRatio * (f.Bold ? 1.08 : 1);
        return new Rect2D(f.X + AlignedLeft(f.Align, width), f.Y - f.Height / 2, width, f.Height);
    }

    private static double AlignedLeft(Rendering.TextAlign align, double width) => align switch
    {
        Rendering.TextAlign.Center => -width / 2,
        Rendering.TextAlign.Right => -width,
        _ => 0,
    };

    /// <summary>Noktanın nesneye isabet edip etmediği (tolerans mm).</summary>
    public static bool HitTest(DrawingObject obj, Point2D p, double tolerance, ISymbolCatalog catalog) => obj switch
    {
        LineObject l => CoordinateTransform.DistanceToSegment(p, l.Start, l.End) <= tolerance + l.Thickness / 2,
        RectangleObject r => HitRectangleOutline(r.Bounds, p, tolerance),
        JunctionObject j => JunctionBounds(j).Inflate(tolerance).Contains(p),
        _ => Get(obj, catalog).Inflate(tolerance).Contains(p),
    };

    private static bool HitRectangleOutline(Rect2D r, Point2D p, double tol)
    {
        if (!r.Inflate(tol).Contains(p))
        {
            return false;
        }
        var inner = r.Inflate(-tol);
        return inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(p);
    }
}
