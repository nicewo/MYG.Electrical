using System.Globalization;
using System.Text;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Devices;

/// <summary>Çizimden cihaz oluşturma seçenekleri (Cihaz oluştur penceresi).</summary>
public sealed class DeviceBuildOptions
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "PLC ve Otomasyon/Kullanıcı";
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>IEC 81346-2 sınıf harfi (PLC için A, röle K...).</summary>
    public string ReferencePrefix { get; set; } = "A";

    /// <summary>
    /// Standalone: cihazın tamamı. Main: çok sayfalı cihazın ana parçası (diğer parçalar etiketin altında listelenir).
    /// Contact: aynı etiketli bir cihazın başka sayfadaki parçası (ana parçanın konumu yazılır).
    /// </summary>
    public SymbolRole Role { get; set; } = SymbolRole.Standalone;

    public Dictionary<string, string> Properties { get; set; } = [];
}

/// <summary>Çizimden oluşturulan sembol + cihaz şablonu. <see cref="Origin"/> sembolün çizimdeki ekleme noktası.</summary>
public sealed record DeviceBuildResult(SymbolDefinition Symbol, DeviceTemplate Template, Point2D Origin, IReadOnlyList<string> Warnings);

/// <summary>
/// EPLAN tarzı cihaz/PLC kutusu tasarımı: seçili çizgi, dikdörtgen, metin ve tasarım pinlerinden (DevicePinObject)
/// JSON'a yazılabilir bir sembol ve onu kullanan cihaz şablonu üretir. Pinler 5 mm grid'de kalır (ekleme noktası
/// grid'e oturtulur), pin kimliği bağlantı tanımıdır (X0.0, Y0.0, L+...).
/// </summary>
public static class DeviceBuilder
{
    public const string UserSymbolPrefix = "user.";
    public const string UserDevicePrefix = "userdev.";

    public static DeviceBuildResult Build(IReadOnlyList<DrawingObject> objects, DeviceBuildOptions options, Func<string, bool> idTaken)
    {
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new DeviceBuildException("Cihaz adı boş olamaz.");
        }
        var pins = objects.OfType<DevicePinObject>().ToList();
        if (pins.Count == 0)
        {
            throw new DeviceBuildException("Seçimde pin yok. Önce Ekle → Cihaz tasarımı menüsünden güç/giriş/çıkış pinlerini yerleştirin.");
        }
        var unnamed = pins.Where(p => string.IsNullOrWhiteSpace(p.Name)).ToList();
        if (unnamed.Count > 0)
        {
            throw new DeviceBuildException($"{unnamed.Count} pinin bağlantı adı yok.");
        }
        var duplicates = pins.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new DeviceBuildException($"Aynı bağlantı adı birden fazla pinde: {string.Join(", ", duplicates)}.");
        }

        // Ekleme noktası: seçimin sol üst köşesi, 5 mm grid'e yuvarlanmış (pinler grid'de kalır).
        var warnings = new List<string>();
        var shapes = objects.Where(o => o is LineObject or RectangleObject or TextObject).ToList();
        var ignored = objects.Count - pins.Count - shapes.Count;
        if (ignored > 0)
        {
            warnings.Add($"{ignored} nesne (sembol, resim, potansiyel...) cihaza alınmadı; yalnızca çizgi, dikdörtgen, metin ve pinler alınır.");
        }
        var minX = objects.Select(o => Bounds(o).Left).Min();
        var minY = objects.Select(o => Bounds(o).Top).Min();
        var origin = new Point2D(Math.Floor(minX / 5) * 5, Math.Floor(minY / 5) * 5);
        // Pinler grid dışındaysa (elle girilmiş X/Y) ekleme noktası ilk pine göre hizalanır
        var first = pins[0];
        var dx = Math.Round(first.X - origin.X, 6) % 5;
        var dy = Math.Round(first.Y - origin.Y, 6) % 5;
        if (Math.Abs(dx) > 1e-6 || Math.Abs(dy) > 1e-6)
        {
            origin = new Point2D(origin.X + dx, origin.Y + dy);
            warnings.Add("Pinler 5 mm grid üzerinde değil; otomatik kablolama için pinleri grid'e oturtun.");
        }

        var primitives = new List<SymbolPrimitive>();
        Point2D L(Point2D p) => new(Math.Round(p.X - origin.X, 4), Math.Round(p.Y - origin.Y, 4));
        foreach (var o in shapes)
        {
            switch (o)
            {
                case LineObject l:
                    var a = L(l.Start);
                    var b = L(l.End);
                    primitives.Add(new LinePrimitive(a.X, a.Y, b.X, b.Y));
                    break;
                case RectangleObject r:
                    var tl = L(new Point2D(r.X, r.Y));
                    primitives.Add(new RectPrimitive(tl.X, tl.Y, r.Width, r.Height) { Dashed = r.Dashed });
                    break;
                case TextObject t when !string.IsNullOrWhiteSpace(t.Text):
                    var tp = L(t.Anchor);
                    primitives.Add(new TextPrimitive(tp.X, tp.Y, t.Text, t.Height) { LeftAligned = true });
                    break;
            }
        }

        var points = new List<ConnectionPoint>();
        foreach (var p in pins.OrderBy(p => p.Function == PinFunction.Output ? 1 : 0).ThenBy(p => p.Y).ThenBy(p => p.X))
        {
            var at = L(p.Anchor);
            var inner = L(p.InnerEnd);
            primitives.Add(new LinePrimitive(at.X, at.Y, inner.X, inner.Y));
            points.Add(new ConnectionPoint(p.Name.Trim(), at.X, at.Y, p.Direction, p.Function)
            {
                Description = string.IsNullOrWhiteSpace(p.Description) ? null : p.Description.Trim(),
            });
        }

        var slug = Slug(options.Name);
        var symbolId = Unique(UserSymbolPrefix + slug, idTaken);
        var deviceId = Unique(UserDevicePrefix + slug, idTaken);
        var category = string.IsNullOrWhiteSpace(options.Category) ? "Kullanıcı" : options.Category.Trim();
        var prefix = string.IsNullOrWhiteSpace(options.ReferencePrefix) ? "A" : options.ReferencePrefix.Trim();
        var symbol = new SymbolDefinition
        {
            Id = symbolId,
            Name = options.Name.Trim(),
            Category = category,
            Description = options.Description.Trim(),
            Keywords = new[] { options.Manufacturer, options.Model }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList(),
            ReferencePrefix = prefix,
            Role = options.Role,
            Primitives = primitives,
            ConnectionPoints = points,
            DefaultProperties = new Dictionary<string, string>(options.Properties),
        };
        var problem = SymbolLibraryLoader.Validate(symbol);
        if (problem is not null)
        {
            throw new DeviceBuildException(problem);
        }
        symbol.Library = "Kullanıcı";
        symbol.IsUserDefined = true;

        var template = new DeviceTemplate
        {
            Id = deviceId,
            Name = options.Name.Trim(),
            Category = category,
            Manufacturer = options.Manufacturer.Trim(),
            Model = options.Model.Trim(),
            OrderNumber = options.OrderNumber.Trim(),
            Description = options.Description.Trim(),
            Symbol = symbolId,
            Keywords = symbol.Keywords,
            Properties = new Dictionary<string, string>(options.Properties),
        };
        template.Library = "Kullanıcı";
        template.IsUserDefined = true;
        return new DeviceBuildResult(symbol, template, origin, warnings);
    }

    private static Rect2D Bounds(DrawingObject o) => o switch
    {
        LineObject l => Rect2D.FromPoints(l.Start, l.End),
        RectangleObject r => r.Bounds,
        _ => new Rect2D(o.Anchor.X, o.Anchor.Y, 0, 0),
    };

    /// <summary>"Delta AS228T" → "delta-as228t" (Türkçe harfler sadeleşir).</summary>
    public static string Slug(string name)
    {
        var map = new Dictionary<char, string> { ['ç'] = "c", ['ğ'] = "g", ['ı'] = "i", ['ö'] = "o", ['ş'] = "s", ['ü'] = "u", ['İ'] = "i" };
        var sb = new StringBuilder();
        foreach (var ch in name.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")))
        {
            if (map.TryGetValue(ch, out var r))
            {
                sb.Append(r);
            }
            else if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }
        var s = sb.ToString().Trim('-');
        return s.Length == 0 ? "cihaz" : s;
    }

    private static string Unique(string id, Func<string, bool> taken)
    {
        if (!taken(id))
        {
            return id;
        }
        for (var i = 2; ; i++)
        {
            var candidate = $"{id}-{i}";
            if (!taken(candidate))
            {
                return candidate;
            }
        }
    }
}

public sealed class DeviceBuildException(string message) : Exception(message);
