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

    /// <summary>Bilgi alanlarının değerleri için proje. Yoksa alan anahtarı yazılır.</summary>
    public Pages.ProjectDocument? Document { get; set; }

    /// <summary>Bilgi alanı: proje bilgisinden değer; logo alanında resim. Gizli alan yalnızca editörde, soluk.</summary>
    private void ComposeField(FieldObject f, ISceneSink sink, SceneComposerOptions options)
    {
        if (!f.Visible && !options.ShowHiddenFields)
        {
            return;
        }
        var color = f.Visible ? Colors.Text : DrawingStyles.MidGray;
        var page = Document?.Pages.FirstOrDefault(p => p.Objects.Contains(f));
        var value = Document is null ? $"{{{f.Field}}}" : Pages.TitleBlockFields.ResolveField(Document, page, f.Field);
        if (f.IsLogo)
        {
            var box = Editing.ObjectBounds.FieldBounds(f, Document);
            if (!string.IsNullOrWhiteSpace(value) && f.Visible)
            {
                sink.DrawImage(value, box);
            }
            else if (options.ShowHiddenFields)
            {
                // Logo yok veya gizli: editörde yer tutucu
                sink.DrawRectangle(box, new StrokeStyle(DrawingStyles.MidGray, DrawingStyles.ThinLine, Dashed: true));
                sink.DrawText("LOGO", box.Center, Math.Min(5, f.Height / 4), DrawingStyles.MidGray, TextAlign.Center);
            }
            return;
        }
        var text = f.Compose(value);
        if (value.Length == 0 && options.ShowHiddenFields)
        {
            // Boş alan editörde görünür (adıyla ya da "[boş]"); çıktıda yalnız etiket kalır
            text = f.ShowLabel && f.Label.Length > 0 ? f.Compose("[boş]") : $"[{Pages.TitleBlockFields.LabelOf(f.Field)}]";
            color = DrawingStyles.MidGray;
        }
        f.RenderedText = text;
        sink.DrawText(text, f.Anchor, f.Height, color, f.Align, f.Bold);
    }

    public void ComposeObject(DrawingObject obj, ISceneSink sink, SceneComposerOptions options)
    {
        switch (obj)
        {
            case LineObject l:
                sink.DrawLine(l.Start, l.End, new StrokeStyle(DrawingStyles.Black, l.Thickness));
                break;
            case RectangleObject r:
                sink.DrawRectangle(r.Bounds, new StrokeStyle(r.Dashed ? Colors.Frame : DrawingStyles.Black, r.Thickness, r.Dashed));
                break;
            case ImageObject img:
                ComposeImage(img, sink);
                break;
            case TextObject t:
                sink.DrawText(t.Text, t.Anchor, t.Height, Colors.Text, t.Align, t.Bold);
                break;
            case FieldObject f:
                ComposeField(f, sink, options);
                break;
            case PotentialObject pot:
                ComposePotential(pot, sink, options);
                break;
            case PageLinkObject link:
                ComposePageLink(link, sink, options);
                break;
            case DevicePinObject pin:
                ComposeDevicePin(pin, sink, options);
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

    /// <summary>Tasarım pini: kutuya uzanan uç çizgisi, uçta küçük daire, bağlantı tanımı ve işlev rengi.</summary>
    private void ComposeDevicePin(DevicePinObject p, ISceneSink sink, SceneComposerOptions options)
    {
        var color = p.Function switch
        {
            PinFunction.Input => Colors.Signal,
            PinFunction.Output => Colors.Communication,
            PinFunction.Power or PinFunction.SupplyPositive => Colors.DcPositive,
            PinFunction.SupplyNegative => Colors.DcNegative,
            PinFunction.ProtectiveEarth => Colors.PE,
            _ => Colors.Symbol,
        };
        sink.DrawLine(p.Anchor, p.InnerEnd, new StrokeStyle(Colors.Symbol, DrawingStyles.SymbolLine));
        sink.DrawEllipse(p.Anchor, 0.7, 0.7, new StrokeStyle(color, DrawingStyles.ThinLine), DrawingStyles.White);
        sink.DrawText(string.IsNullOrEmpty(p.Name) ? "?" : p.Name, PinLabelPosition(p.Pin), DrawingStyles.PinTextHeight, color, TextAlign.Left, bold: true);
        _ = options;
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
        var targets = Network?.LinkDisplayTargets(link) ?? [];
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
                    var text = s is DeviceObject device && t.Text.Contains('{') ? ResolveFields(t.Text, device) : t.Text;
                    sink.DrawText(text, s.LocalToGlobal(new(t.X, t.Y)), t.Height * s.Scale, Colors.Symbol, t.LeftAligned ? TextAlign.Left : TextAlign.Center);
                    break;
            }
        }
    }

    private void ComposeDeviceAnnotations(DeviceObject d, ISceneSink sink, SceneComposerOptions options)
    {
        var bounds = Editing.ObjectBounds.SymbolBounds(d, catalog);
        if (!string.IsNullOrWhiteSpace(d.Reference))
        {
            ComposeReferenceBlock(d, bounds, sink);
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
    /// Etiket bloğu: etiket (kalın), işlev metni ve çapraz referanslar alt alta. Sembolün tanımladığı konuma yazılır:
    /// sağda (varsayılan), solda (EPLAN'daki gibi dikey bobin/kontak) veya üstte (dar ve uzun PLC modülleri).
    /// </summary>
    private void ComposeReferenceBlock(DeviceObject d, Rect2D bounds, ISceneSink sink)
    {
        const double h = ConnectorGeometry.CrossReferenceHeight;
        var lines = new List<(string Text, double Height, bool Bold)> { (d.Reference, DrawingStyles.ReferenceTextHeight, true) };
        if (!string.IsNullOrWhiteSpace(d.Description))
        {
            // İşlev metni (EPLAN "işlev tanımı"): PLC kanalının görevi, cihazın adı...
            lines.Add((d.Description, h, false));
        }
        lines.AddRange(CrossReferenceLines(d).Select(t => (t, h, false)));

        var position = catalog.Find(d.SymbolId)?.ReferencePosition ?? ReferencePosition.Right;
        if (position == ReferencePosition.InSymbol)
        {
            return; // sembolün metin alanları gösteriyor
        }
        // Satır aralığı: etiketten sonra yarım mm, küçük satırlar arasında yazı boyunun %30'u
        static double Step((string Text, double Height, bool Bold) line) => line.Bold ? line.Height + 0.5 : line.Height * 1.3;
        Point2D start;
        TextAlign align;
        switch (position)
        {
            case ReferencePosition.Left:
                start = new Point2D(bounds.Left - ReferenceGap, bounds.Center.Y);
                align = TextAlign.Right;
                break;
            case ReferencePosition.Top:
                var total = lines.Take(lines.Count - 1).Sum(Step);
                start = new Point2D(bounds.Center.X, bounds.Top - 2.0 - total);
                align = TextAlign.Center;
                break;
            default:
                start = new Point2D(bounds.Right + ReferenceGap, bounds.Center.Y);
                align = TextAlign.Left;
                break;
        }
        var y = start.Y;
        foreach (var line in lines)
        {
            sink.DrawText(line.Text, new Point2D(start.X, y), line.Height, Colors.Text, align, bold: line.Bold);
            y += Step(line);
        }
    }

    /// <summary>Sembol metnindeki {Reference}, {Description}, {Pin:id}, {CrossReference} alanlarını doldurur.</summary>
    private string ResolveFields(string text, DeviceObject d) =>
        FieldPattern.Replace(text, m => m.Groups[1].Value switch
        {
            "Reference" => d.Reference,
            "Description" => d.Description,
            "Pin" when m.Groups[2].Success => d.PinLabel(m.Groups[2].Value),
            "CrossReference" => string.Join(" ", CrossReferenceLines(d)),
            _ => m.Value,
        });

    private static readonly System.Text.RegularExpressions.Regex FieldPattern =
        new(@"\{(Reference|Description|Pin|CrossReference)(?::([^}]+))?\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Çapraz referans: ana fonksiyonun (bobin, PLC CPU...) etiketi altında parçalarının listesi (kontak aynası),
    /// kontak/parçanın etiketinin altında ana fonksiyonun sayfa.kolon konumu.
    /// </summary>
    private IReadOnlyList<string> CrossReferenceLines(DeviceObject d)
    {
        if (Network is null || catalog.Find(d.SymbolId) is not { } def)
        {
            return [];
        }
        if (def.Role == SymbolRole.Main)
        {
            return Core.Network.CrossReferences.ContactMirror(Network, catalog, d);
        }
        if (def.Role == SymbolRole.Contact)
        {
            var main = Core.Network.CrossReferences.MainLocation(Network, catalog, d);
            return main.Length > 0 ? [main] : [];
        }
        return [];
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
