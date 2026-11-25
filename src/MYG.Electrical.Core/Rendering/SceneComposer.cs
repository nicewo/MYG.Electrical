using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Rendering;

/// <summary>
/// Çizim nesnelerini hedeften bağımsız temel şekillere dönüştürür.
/// Ekran (WPF) ve PDF çıktısı aynı kompozisyonu kullanır; böylece çıktı ekranla birebir aynıdır.
/// </summary>
public sealed class SceneComposer(ISymbolCatalog catalog, Func<string, Resources.ProjectResource?>? resources = null)
{
    private const double PinLabelOffset = 1.0;
    private const double PinLabelAlong = 2.2;
    /// <summary>Tanım etiketi ile sembol arası boşluk; pin numaralarına yer bırakır.</summary>
    private const double ReferenceGap = 5.0;

    public void ComposeObject(DrawingObject obj, ISceneSink sink, SceneComposerOptions options)
    {
        switch (obj)
        {
            case LineObject l:
                sink.DrawLine(l.Start, l.End, new StrokeStyle(DrawingStyles.Black, l.Thickness));
                break;
            case RectangleObject r:
                sink.DrawRectangle(r.Bounds, new StrokeStyle(DrawingStyles.Black, r.Thickness));
                break;
            case ImageObject img:
                ComposeImage(img, sink);
                break;
            case TextObject t:
                sink.DrawText(t.Text, t.Anchor, t.Height, DrawingStyles.Black);
                break;
            case JunctionObject j:
                ComposeJunction(j, sink, options);
                break;
            case DeviceObject d:
                ComposeSymbol(d, sink);
                ComposeDeviceAnnotations(d, sink, options);
                break;
            case SymbolObject s:
                ComposeSymbol(s, sink);
                break;
        }
    }

    public void ComposeObjects(IEnumerable<DrawingObject> objects, ISceneSink sink, SceneComposerOptions options)
    {
        foreach (var o in objects)
        {
            ComposeObject(o, sink, options);
        }
    }

    private void ComposeImage(ImageObject img, ISceneSink sink)
    {
        var id = string.IsNullOrEmpty(img.RenderResourceId) ? img.SourceResourceId : img.RenderResourceId;
        if (resources?.Invoke(id) is { } res)
        {
            sink.DrawImageData(res.Id, res.Data, img.Bounds);
            return;
        }
        // Kaynak bulunamadı: çarpılı yer tutucu
        var missing = new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine, Dashed: true);
        var b = img.Bounds;
        sink.DrawRectangle(b, missing);
        sink.DrawLine(new(b.Left, b.Top), new(b.Right, b.Bottom), missing);
        sink.DrawLine(new(b.Right, b.Top), new(b.Left, b.Bottom), missing);
    }

    private static void ComposeJunction(JunctionObject j, ISceneSink sink, SceneComposerOptions options)
    {
        if (options.ShowConnectionPoints)
        {
            // Yalnızca editörde: hangi yönlere bağlantı yapılabildiğini gösteren kısa çizgiler
            var stub = new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine);
            foreach (var d in j.ActiveDirections)
            {
                var end = d switch
                {
                    PinDirection.Left => new Point2D(-DrawingStyles.JunctionStubLength, 0),
                    PinDirection.Right => new Point2D(DrawingStyles.JunctionStubLength, 0),
                    PinDirection.Up => new Point2D(0, -DrawingStyles.JunctionStubLength),
                    _ => new Point2D(0, DrawingStyles.JunctionStubLength),
                };
                sink.DrawLine(j.Anchor, j.Anchor + end, stub);
            }
        }
        if (j.ShowsDot)
        {
            sink.DrawEllipse(j.Anchor, j.Size / 2, j.Size / 2, null, DrawingStyles.Black);
        }
    }

    public static void ComposeWires(IEnumerable<WireConnection> wires, ISceneSink sink)
    {
        foreach (var w in wires)
        {
            sink.DrawLine(w.Start, w.End, DrawingStyles.Wire);
        }
    }

    private void ComposeSymbol(SymbolObject s, ISceneSink sink)
    {
        if (catalog.Find(s.SymbolId) is not { } def)
        {
            // Bilinmeyen sembol: yer tutucu kutu
            sink.DrawRectangle(Editing.ObjectBounds.SymbolBounds(s, catalog), new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine, true));
            return;
        }

        foreach (var prim in def.Primitives)
        {
            var stroke = DrawingStyles.Symbol with { Dashed = prim.Dashed, Thickness = DrawingStyles.SymbolLine * s.Scale };
            switch (prim)
            {
                case LinePrimitive l:
                    sink.DrawLine(s.LocalToGlobal(new(l.X1, l.Y1)), s.LocalToGlobal(new(l.X2, l.Y2)), stroke);
                    break;
                case RectPrimitive r:
                    var rect = CoordinateTransform.TransformBounds(new Rect2D(r.X, r.Y, r.Width, r.Height), s.Position, s.Rotation, s.Scale);
                    sink.DrawRectangle(rect, stroke, r.Filled ? DrawingStyles.Black : null);
                    break;
                case CirclePrimitive c:
                    var radius = c.Radius * s.Scale;
                    sink.DrawEllipse(s.LocalToGlobal(new(c.CenterX, c.CenterY)), radius, radius, stroke, c.Filled ? DrawingStyles.Black : null);
                    break;
                case ArcPrimitive a:
                    sink.DrawArc(s.LocalToGlobal(new(a.CenterX, a.CenterY)), a.Radius * s.Scale, a.StartAngle + s.Rotation, a.SweepAngle, stroke);
                    break;
                case TextPrimitive t:
                    sink.DrawText(t.Text, s.LocalToGlobal(new(t.X, t.Y)), t.Height * s.Scale, DrawingStyles.Black, TextAlign.Center);
                    break;
            }
        }
    }

    private void ComposeDeviceAnnotations(DeviceObject d, ISceneSink sink, SceneComposerOptions options)
    {
        var bounds = Editing.ObjectBounds.SymbolBounds(d, catalog);
        if (!string.IsNullOrWhiteSpace(d.Reference))
        {
            var pos = new Point2D(bounds.Right + ReferenceGap, bounds.Center.Y);
            sink.DrawText(d.Reference, pos, DrawingStyles.ReferenceTextHeight, DrawingStyles.ReferenceColor, TextAlign.Left, bold: true);
        }

        foreach (var pin in d.GetGlobalPins())
        {
            if (options.ShowConnectionPoints)
            {
                var m = DrawingStyles.PinMarkerSize;
                sink.DrawEllipse(pin.Position, m, m, new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine));
            }
            if (options.ShowPinLabels && !string.IsNullOrEmpty(pin.PinId))
            {
                sink.DrawText(pin.PinId, PinLabelPosition(pin), DrawingStyles.PinTextHeight, DrawingStyles.DarkGray);
            }
        }
    }

    /// <summary>Pin numarasını, pinin baktığı yönün tersine (sembolün içine doğru) ve yanına yerleştirir.</summary>
    private static Point2D PinLabelPosition(GlobalPin pin) => pin.Direction switch
    {
        PinDirection.Up => pin.Position + new Point2D(PinLabelOffset, PinLabelAlong),
        PinDirection.Down => pin.Position + new Point2D(PinLabelOffset, -PinLabelAlong),
        PinDirection.Left => pin.Position + new Point2D(PinLabelOffset, -PinLabelAlong),
        _ => pin.Position + new Point2D(-PinLabelAlong - PinLabelOffset, -PinLabelAlong),
    };
}
