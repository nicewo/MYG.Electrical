using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Symbols;

/// <summary>Sembol tanımlarını okunabilir biçimde oluşturmak için akıcı yardımcı.</summary>
internal sealed class SymbolBuilder(string id, string name, string category, string prefix)
{
    /// <summary>Standart dikey sembolde pinlerin merkezden uzaklığı (mm, 5 mm grid'in katı).</summary>
    public const double PinReach = 10.0;

    private readonly List<SymbolPrimitive> _primitives = [];
    private readonly List<ConnectionPoint> _pins = [];

    public SymbolBuilder Line(double x1, double y1, double x2, double y2, bool dashed = false)
    {
        _primitives.Add(new LinePrimitive(x1, y1, x2, y2) { Dashed = dashed });
        return this;
    }

    public SymbolBuilder Poly(bool closed, params (double X, double Y)[] pts)
    {
        for (var i = 0; i < pts.Length - 1; i++)
        {
            Line(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y);
        }
        if (closed && pts.Length > 2)
        {
            Line(pts[^1].X, pts[^1].Y, pts[0].X, pts[0].Y);
        }
        return this;
    }

    public SymbolBuilder Rect(double x, double y, double w, double h, bool filled = false)
    {
        _primitives.Add(new RectPrimitive(x, y, w, h) { Filled = filled });
        return this;
    }

    public SymbolBuilder Circle(double cx, double cy, double r, bool filled = false)
    {
        _primitives.Add(new CirclePrimitive(cx, cy, r) { Filled = filled });
        return this;
    }

    public SymbolBuilder Arc(double cx, double cy, double r, double start, double sweep)
    {
        _primitives.Add(new ArcPrimitive(cx, cy, r, start, sweep));
        return this;
    }

    public SymbolBuilder Text(double x, double y, string text, double height)
    {
        _primitives.Add(new TextPrimitive(x, y, text, height));
        return this;
    }

    public SymbolBuilder Pin(string pinId, double x, double y, PinDirection direction)
    {
        _pins.Add(new ConnectionPoint(pinId, x, y, direction));
        return this;
    }

    // ---------- Tekrar kullanılan IEC parçaları ----------

    private const double LeadEnd = 4.0;
    private const double BladeTipX = 3.5;
    private const double BladeTipY = 4.5;

    /// <summary>Kapanan (NO) kontak; pinler (ox, ±PinReach).</summary>
    public SymbolBuilder NormallyOpenContact(double ox, string topPin, string bottomPin)
    {
        Line(ox, -PinReach, ox, -LeadEnd);
        Line(ox, PinReach, ox, LeadEnd);
        Line(ox, LeadEnd, ox - BladeTipX, -BladeTipY);
        return Pin(topPin, ox, -PinReach, PinDirection.Up).Pin(bottomPin, ox, PinReach, PinDirection.Down);
    }

    /// <summary>Açan (NC) kontak; pinler (ox, ±PinReach).</summary>
    public SymbolBuilder NormallyClosedContact(double ox, string topPin, string bottomPin)
    {
        Line(ox, -PinReach, ox, -LeadEnd);
        Line(ox, -LeadEnd, ox + LeadEnd, -LeadEnd);
        Line(ox, PinReach, ox, LeadEnd);
        Line(ox, LeadEnd, ox + BladeTipX + 1, -BladeTipY - 0.5);
        return Pin(topPin, ox, -PinReach, PinDirection.Up).Pin(bottomPin, ox, PinReach, PinDirection.Down);
    }

    /// <summary>Kontak bıçağının orta noktası (mekanik bağlantı çizgisi buradan çıkar).</summary>
    public static double BladeMidX(double ox) => ox - BladeTipX / 2;

    public SymbolDefinition Build() => new()
    {
        Id = id,
        Name = name,
        Category = category,
        ReferencePrefix = prefix,
        Primitives = _primitives.ToArray(),
        ConnectionPoints = _pins.ToArray(),
    };
}
