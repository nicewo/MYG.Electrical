using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Symbols;

/// <summary>
/// IEC 60617 esinli yerleşik sembol kütüphanesi. Tüm ölçüler mm; sembol orijini ekleme noktasıdır.
/// Pinler 5 mm grid üzerinde olacak şekilde tasarlanmıştır, böylece hizalı yerleştirilen cihazlar otomatik bağlanır.
/// </summary>
public sealed class BuiltInSymbolLibrary : ISymbolCatalog
{
    public const string CategoryPower = "Güç";
    public const string CategoryControl = "Kumanda";
    public const string CategorySignal = "Sinyal";
    public const string CategoryConnection = "Bağlantı";

    private const double R = SymbolBuilder.PinReach;
    private readonly Dictionary<string, SymbolDefinition> _byId;

    public static BuiltInSymbolLibrary Instance { get; } = new();

    public BuiltInSymbolLibrary()
    {
        All = CreateAll();
        _byId = All.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<SymbolDefinition> All { get; }

    public SymbolDefinition? Find(string symbolId) => _byId.GetValueOrDefault(symbolId);

    private static SymbolDefinition[] CreateAll() =>
    [
        Contactor(), CircuitBreaker(), Fuse(), Motor(),
        Coil(), Relay(), NormallyOpen(), NormallyClosed(),
        PushButton(), EmergencyStop(), LimitSwitch(),
        Sensor(), Lamp(),
        Terminal(),
    ];

    private static SymbolDefinition Contactor()
    {
        const double pitch = 10;
        var b = new SymbolBuilder(SymbolIds.Contactor, "Kontaktör (3 kutup)", CategoryPower, "K");
        string[][] pins = [["1", "2"], ["3", "4"], ["5", "6"]];
        for (var i = 0; i < pins.Length; i++)
        {
            var ox = (i - 1) * pitch;
            b.NormallyOpenContact(ox, pins[i][0], pins[i][1]);
            b.Arc(ox, -4, 1.2, 0, 180); // kontaktör fonksiyon işareti
        }
        return b.Line(SymbolBuilder.BladeMidX(-pitch), 0, SymbolBuilder.BladeMidX(pitch), 0, dashed: true).Build();
    }

    private static SymbolDefinition CircuitBreaker()
    {
        const double pitch = 10;
        var b = new SymbolBuilder(SymbolIds.CircuitBreaker, "Otomatik sigorta (3 kutup)", CategoryPower, "Q");
        string[][] pins = [["1", "2"], ["3", "4"], ["5", "6"]];
        for (var i = 0; i < pins.Length; i++)
        {
            var ox = (i - 1) * pitch;
            b.NormallyOpenContact(ox, pins[i][0], pins[i][1]);
            b.Line(ox - 1, -5, ox + 1, -3).Line(ox + 1, -5, ox - 1, -3); // kesici işareti
        }
        return b.Line(SymbolBuilder.BladeMidX(-pitch) - 6, 0, SymbolBuilder.BladeMidX(pitch), 0, dashed: true)
            .Rect(SymbolBuilder.BladeMidX(-pitch) - 9, -1.5, 3, 3)
            .Build();
    }

    private static SymbolDefinition Fuse() =>
        new SymbolBuilder(SymbolIds.Fuse, "Sigorta", CategoryPower, "F")
            .Line(0, -R, 0, R)
            .Rect(-2, -5, 4, 10)
            .Pin("1", 0, -R, PinDirection.Up)
            .Pin("2", 0, R, PinDirection.Down)
            .Build();

    private static SymbolDefinition Motor()
    {
        // Pin aralığı kontaktör/otomatik sigorta kutup aralığı (10 mm) ile aynı: alt alta konunca 3 faz otomatik bağlanır.
        const double cy = 8, r = 8, pitch = 10, entry = 5, bend = -3;
        var edgeY = cy - Math.Sqrt(r * r - entry * entry);
        return new SymbolBuilder(SymbolIds.Motor, "Motor (3 faz)", CategoryPower, "M")
            .Circle(0, cy, r)
            .Text(0, cy - 1.5, "M", 5)
            .Text(0, cy + 3.5, "3~", 3)
            .Line(-pitch, -R, -pitch, bend).Line(-pitch, bend, -entry, edgeY)
            .Line(0, -R, 0, cy - r)
            .Line(pitch, -R, pitch, bend).Line(pitch, bend, entry, edgeY)
            .Pin("U1", -pitch, -R, PinDirection.Up)
            .Pin("V1", 0, -R, PinDirection.Up)
            .Pin("W1", pitch, -R, PinDirection.Up)
            .Build();
    }

    private static SymbolDefinition Coil() =>
        new SymbolBuilder(SymbolIds.Coil, "Bobin", CategoryControl, "K")
            .Rect(-5, -3, 10, 6)
            .Line(0, -R, 0, -3).Line(0, 3, 0, R)
            .Pin("A1", 0, -R, PinDirection.Up)
            .Pin("A2", 0, R, PinDirection.Down)
            .Build();

    private static SymbolDefinition Relay()
    {
        const double contactX = 15;
        return new SymbolBuilder(SymbolIds.Relay, "Röle (bobin + NO)", CategoryControl, "K")
            .Rect(-5, -3, 10, 6)
            .Line(0, -R, 0, -3).Line(0, 3, 0, R)
            .Pin("A1", 0, -R, PinDirection.Up)
            .Pin("A2", 0, R, PinDirection.Down)
            .NormallyOpenContact(contactX, "13", "14")
            .Line(5, 0, SymbolBuilder.BladeMidX(contactX), 0, dashed: true)
            .Build();
    }

    private static SymbolDefinition NormallyOpen() =>
        new SymbolBuilder(SymbolIds.NormallyOpen, "Açık kontak (NO)", CategoryControl, "K")
            .NormallyOpenContact(0, "13", "14").Build();

    private static SymbolDefinition NormallyClosed() =>
        new SymbolBuilder(SymbolIds.NormallyClosed, "Kapalı kontak (NC)", CategoryControl, "K")
            .NormallyClosedContact(0, "11", "12").Build();

    private static SymbolDefinition PushButton()
    {
        const double head = -8;
        return new SymbolBuilder(SymbolIds.PushButton, "Buton (NO)", CategoryControl, "S")
            .NormallyOpenContact(0, "13", "14")
            .Line(head, 0, SymbolBuilder.BladeMidX(0), 0, dashed: true)
            .Line(head, -2.5, head, 2.5).Line(head, -2.5, head + 1, -2.5).Line(head, 2.5, head + 1, 2.5)
            .Build();
    }

    private static SymbolDefinition EmergencyStop()
    {
        const double stem = -7;
        return new SymbolBuilder(SymbolIds.EmergencyStop, "Acil stop (NC)", CategoryControl, "S")
            .NormallyClosedContact(0, "11", "12")
            .Line(stem, 0, SymbolBuilder.BladeMidX(0) + 2, 0, dashed: true)
            .Line(stem, -3, stem, 3)
            .Arc(stem, 0, 3, 90, 180) // mantar başlık
            .Build();
    }

    private static SymbolDefinition LimitSwitch()
    {
        const double tip = -6;
        return new SymbolBuilder(SymbolIds.LimitSwitch, "Limit switch (NO)", CategoryControl, "S")
            .NormallyOpenContact(0, "13", "14")
            .Line(tip, 0, SymbolBuilder.BladeMidX(0), 0, dashed: true)
            .Poly(true, (tip, -2), (tip - 3, 0), (tip, 2))
            .Build();
    }

    private static SymbolDefinition Sensor()
    {
        const double d = 6;
        return new SymbolBuilder(SymbolIds.Sensor, "Endüktif sensör (PNP)", CategorySignal, "B")
            .Poly(true, (0, -d), (d, 0), (0, d), (-d, 0))
            .Text(0, 0, "PNP", 2)
            .Line(0, -R, 0, -d).Line(0, d, 0, R).Line(d, 0, R, 0)
            .Pin("BN", 0, -R, PinDirection.Up)
            .Pin("BU", 0, R, PinDirection.Down)
            .Pin("BK", R, 0, PinDirection.Right)
            .Build();
    }

    private static SymbolDefinition Lamp()
    {
        const double r = 4;
        var k = r * Math.Sqrt(0.5);
        return new SymbolBuilder(SymbolIds.Lamp, "Sinyal lambası", CategorySignal, "H")
            .Circle(0, 0, r)
            .Line(-k, -k, k, k).Line(-k, k, k, -k)
            .Line(0, -R, 0, -r).Line(0, r, 0, R)
            .Pin("X1", 0, -R, PinDirection.Up)
            .Pin("X2", 0, R, PinDirection.Down)
            .Build();
    }

    private static SymbolDefinition Terminal()
    {
        const double reach = 5, r = 1.5;
        return new SymbolBuilder(SymbolIds.Terminal, "Klemens", CategoryConnection, "X")
            .Circle(0, 0, r)
            .Line(0, -reach, 0, -r).Line(0, r, 0, reach)
            .Pin("1", 0, -reach, PinDirection.Up)
            .Pin("2", 0, reach, PinDirection.Down)
            .Build();
    }
}
