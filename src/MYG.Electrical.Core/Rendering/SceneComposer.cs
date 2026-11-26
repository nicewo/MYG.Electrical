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

    /// <summary>Proje ağı: kablo/potansiyel renkleri ve sayfa bağlantısı çapraz referansları için. Yoksa varsayılan renk.</summary>
    public Network.ProjectNetwork? Network { get; set; }

    public ColorScheme Colors { get; set; } = ColorScheme.Default;

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
                sink.DrawText(t.Text, t.Anchor, t.Height, Colors.Text);
                break;
            case PotentialObject pot:
                ComposePotential(pot, sink, options);
                break;
            case PageLinkObject link:
                ComposePageLink(link, sink, options);
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
        if (resources?.Invoke(id) is { Data.Length: > 0 } res && sink.DrawImageData(res.Id, res.Data, img.Bounds))
        {
            return;
        }
        // Kaynak yok veya resim çözülemedi: çarpılı yer tutucu
        var missing = new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine, Dashed: true);
        var b = img.Bounds;
        sink.DrawRectangle(b, missing);
        sink.DrawLine(new(b.Left, b.Top), new(b.Right, b.Bottom), missing);
        sink.DrawLine(new(b.Right, b.Top), new(b.Left, b.Bottom), missing);
    }

    private void ComposeJunction(JunctionObject j, ISceneSink sink, SceneComposerOptions options)
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
            sink.DrawEllipse(j.Anchor, j.Size / 2, j.Size / 2, null, Colors.Text);
        }
    }

    /// <summary>Kabloları potansiyel rengine göre çizer; kabloya numara girilmişse yanına yazar.</summary>
    public void ComposeWires(Pages.Page? page, IEnumerable<WireConnection> wires, ISceneSink sink)
    {
        foreach (var w in wires)
        {
            var color = Colors.ForPotential(Network?.PotentialOf(w)?.Kind);
            sink.DrawLine(w.Start, w.End, new StrokeStyle(color, DrawingStyles.WireLine));
            if (page?.FindWireData(w.From, w.To) is { Number.Length: > 0 } data)
            {
                var mid = new Point2D((w.Start.X + w.End.X) / 2, (w.Start.Y + w.End.Y) / 2);
                if (w.IsHorizontal)
                {
                    sink.DrawText(data.Number, mid + new Point2D(0, -1.6), ConnectorGeometry.WireNumberHeight, Colors.Text, TextAlign.Center);
                }
                else
                {
                    sink.DrawText(data.Number, mid + new Point2D(0.8, 0), ConnectorGeometry.WireNumberHeight, Colors.Text);
                }
            }
        }
    }

    /// <summary>Ağ bilgisi olmadan (tek renk) kablo çizimi.</summary>
    public static void ComposeWires(IEnumerable<WireConnection> wires, ISceneSink sink) =>
        new SceneComposer(new Symbols.SymbolCatalog([])).ComposeWires(null, wires, sink);

    private void ComposePotential(PotentialObject p, ISceneSink sink, SceneComposerOptions options)
    {
        var color = Colors.ForPotential(p.PotentialKind);
        var stroke = new StrokeStyle(color, DrawingStyles.SymbolLine);
        var top = p.LocalToGlobal(new(0, -ConnectorGeometry.PotentialHeight));
        sink.DrawLine(p.Anchor, top, stroke);
        sink.DrawLine(p.LocalToGlobal(new(-ConnectorGeometry.PotentialBarHalf, -ConnectorGeometry.PotentialHeight)),
            p.LocalToGlobal(new(ConnectorGeometry.PotentialBarHalf, -ConnectorGeometry.PotentialHeight)), stroke);
        // Ad her zaman yatay ve işaretin dış tarafında okunur
        var outward = p.LocalToGlobal(new(0, -ConnectorGeometry.PotentialHeight - ConnectorGeometry.NameHeight * 0.8));
        sink.DrawText(p.Name, outward, ConnectorGeometry.NameHeight, color, TextAlign.Center, bold: true);
        if (options.ShowConnectionPoints)
        {
            var m = DrawingStyles.PinMarkerSize;
            sink.DrawEllipse(p.Anchor, m, m, new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine));
        }
    }

    private void ComposePageLink(PageLinkObject link, ISceneSink sink, SceneComposerOptions options)
    {
        var color = Colors.ForPotential(Network?.PotentialOf(link.Pin.Ref)?.Kind);
        var stroke = new StrokeStyle(color, DrawingStyles.SymbolLine);
        const double len = ConnectorGeometry.LinkArrowLength, half = ConnectorGeometry.LinkArrowHalf;
        sink.DrawPolygon(
        [
            link.LocalToGlobal(new(0, -half)), link.LocalToGlobal(new(len - half, -half)), link.LocalToGlobal(new(len, 0)),
            link.LocalToGlobal(new(len - half, half)), link.LocalToGlobal(new(0, half)),
        ], closed: true, stroke);
        var targets = Network?.LinkTargets(link) ?? [];
        var text = targets.Count == 0 ? link.Name : $"{link.Name}  {string.Join(" ", targets)}";
        var textPos = link.LocalToGlobal(new(len + 1.5, 0));
        var align = link.Direction switch
        {
            PinDirection.Right => TextAlign.Right, // dönmüş: ok sola bakar
            PinDirection.Up or PinDirection.Down => TextAlign.Left,
            _ => TextAlign.Left,
        };
        if (link.Direction is PinDirection.Up or PinDirection.Down)
        {
            textPos += new Point2D(1.5, 0);
        }
        sink.DrawText(text, textPos, ConnectorGeometry.CrossReferenceHeight, Colors.Text, align);
        if (options.ShowConnectionPoints)
        {
            var m = DrawingStyles.PinMarkerSize;
            sink.DrawEllipse(link.Anchor, m, m, new StrokeStyle(DrawingStyles.PinMarker, DrawingStyles.ThinLine));
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
            var stroke = new StrokeStyle(Colors.Symbol, DrawingStyles.SymbolLine * s.Scale, prim.Dashed);
            switch (prim)
            {
                case LinePrimitive l:
                    sink.DrawLine(s.LocalToGlobal(new(l.X1, l.Y1)), s.LocalToGlobal(new(l.X2, l.Y2)), stroke);
                    break;
                case RectPrimitive r:
                    var rect = CoordinateTransform.TransformBounds(new Rect2D(r.X, r.Y, r.Width, r.Height), s.Position, s.Rotation, s.Scale);
                    sink.DrawRectangle(rect, stroke, r.Filled ? Colors.Symbol : null);
                    break;
                case CirclePrimitive c:
                    var radius = c.Radius * s.Scale;
                    sink.DrawEllipse(s.LocalToGlobal(new(c.CenterX, c.CenterY)), radius, radius, stroke, c.Filled ? Colors.Symbol : null);
                    break;
                case ArcPrimitive a:
                    sink.DrawArc(s.LocalToGlobal(new(a.CenterX, a.CenterY)), a.Radius * s.Scale, a.StartAngle + s.Rotation, a.SweepAngle, stroke);
                    break;
                case PolygonPrimitive poly:
                    var pts = poly.Vertices().Select(v => s.LocalToGlobal(v)).ToList();
                    sink.DrawPolygon(pts, poly.Closed, stroke, poly.Filled ? stroke.Color : null);
                    break;
                case TextPrimitive t:
                    sink.DrawText(t.Text, s.LocalToGlobal(new(t.X, t.Y)), t.Height * s.Scale, Colors.Symbol, TextAlign.Center);
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
            sink.DrawText(d.Reference, pos, DrawingStyles.ReferenceTextHeight, Colors.Text, TextAlign.Left, bold: true);
            ComposeCrossReferences(d, bounds, pos, sink);
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
                sink.DrawText(d.PinLabel(pin.PinId), PinLabelPosition(pin), DrawingStyles.PinTextHeight, DrawingStyles.DarkGray);
            }
        }
    }

    /// <summary>
    /// Çapraz referans: ana fonksiyonun (bobin, PLC CPU...) etiketi altında parçalarının listesi (kontak aynası),
    /// kontak/parçanın etiketinin altında ana fonksiyonun sayfa.kolon konumu.
    /// </summary>
    private void ComposeCrossReferences(DeviceObject d, Rect2D bounds, Point2D referencePos, ISceneSink sink)
    {
        if (Network is null || catalog.Find(d.SymbolId) is not { } def)
        {
            return;
        }
        const double h = ConnectorGeometry.CrossReferenceHeight;
        if (def.Role == SymbolRole.Main)
        {
            var lines = Core.Network.CrossReferences.ContactMirror(Network, catalog, d);
            // Etiketin altında, kablonun geçtiği eksenin yanında (yazı hattın üstüne binmesin)
            var y = referencePos.Y + DrawingStyles.ReferenceTextHeight + 0.5;
            foreach (var line in lines)
            {
                sink.DrawText(line, new Point2D(referencePos.X, y), h, Colors.Text);
                y += h * 1.3;
            }
        }
        else if (def.Role == SymbolRole.Contact)
        {
            var main = Core.Network.CrossReferences.MainLocation(Network, catalog, d);
            if (main.Length > 0)
            {
                sink.DrawText(main, referencePos + new Point2D(0, DrawingStyles.ReferenceTextHeight + 0.5), h, Colors.Text);
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
