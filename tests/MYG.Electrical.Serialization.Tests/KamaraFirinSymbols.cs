using System.Globalization;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>
/// Kamara fırını örnek projesinin projeye gömülü sembolleri: EPLAN'daki kara kutu cihazların (analizör, faz rölesi,
/// sürücü, PLC CPU ve Beijer modülleri), klemens şeritlerinin ve EPLAN tarzı PLC kanal kutularının karşılıkları.
/// Hepsi "user.kamara-" önekli kullanıcı sembolleridir; proje dosyasıyla birlikte taşınır.
/// </summary>
public static class KamaraFirinSymbols
{
    public const string Prefix = "user.kamara-";
    private const string Category = "Kamara Fırın";
    private const double PinCircle = 0.8;

    public static string PlcBoxId(bool input, char side) => $"{Prefix}plc-{(input ? "giris" : "cikis")}-{char.ToLowerInvariant(side)}";

    public static string StripId(string name) => $"{Prefix}klemens-{name.ToLowerInvariant()}";

    public static string ModuleId(string reference) => $"{Prefix}modul-{reference.TrimStart('-').ToLowerInvariant()}";

    public static List<SymbolDefinition> All()
    {
        var list = new List<SymbolDefinition>
        {
            Supply(), Analyzer(), PhaseRelay(), CurrentTransformer(), PowerSupply(), EthernetSwitch(), PanelPc(),
            LitButton(nc: false), LitButton(nc: true), EmergencyStop(), StackLight(), Drive(), Cpu(), Coupler(),
            Coil(), ContactNo(ReferencePosition.Left, "kontak-no"), ContactNo(ReferencePosition.Top, "kontak-no-ust"), Mcb4p(),
        };
        foreach (var input in new[] { true, false })
        {
            foreach (var side in "TBLR")
            {
                list.Add(PlcBox(input, side));
            }
        }
        // Beijer G/Ç modülleri (adresler sembolün üstünde yazılı)
        list.Add(Module("-PLC3", "GT-3114", "AI", ["PIW0", "PIW2", "PIW4", "PIW6"], input: true));
        list.Add(Module("-PLC4", "GT-3114", "AI", ["PIW8", "PIW10", "PIW12", "PIW14"], input: true));
        list.Add(Module("-PLC5", "GT-4114", "AQ", ["PQW0", "PQW2", "PQW4", "PQW6"], input: false));
        list.Add(Module("-PLC6", "GT-4114", "AQ", ["PQW8", "PQW10", "PQW12", "PQW14"], input: false));
        list.Add(Module("-PLC7", "GT-3804", "AI", ["PIW16", "PIW18", "PIW20", "PIW22"], input: true));
        list.Add(Module("-PLC8", "GT-12DF", "DI", Bits("I", 2), input: true));
        list.Add(Module("-PLC9", "GT-12DF", "DI", Bits("I", 4), input: true));
        list.Add(Module("-PLC10", "GT-12DF", "DI", Bits("I", 6), input: true));
        list.Add(Module("-PLC11", "GT-12DF", "DI", Bits("I", 8), input: true));
        list.Add(Module("-PLC12", "GT-228F", "DQ", Bits("Q", 2), input: false));
        list.Add(Module("-PLC13", "GT-228F", "DQ", Bits("Q", 4), input: false));
        list.Add(Module("-PLC14", "GT-228F", "DQ", Bits("Q", 6), input: false));

        // Klemens şeritleri
        list.Add(BridgedStrip("X1", 10, left: [1, 2], right: [1, 2, 3, 4, 6, 8]));
        list.Add(BridgedStrip("X2", 10, left: [1, 2], right: [1, 2, 3, 5, 6, 7, 8, 9]));
        list.Add(ThroughStrip("motor", 3, 10));
        list.Add(TopStrip("brulor", [1, 2, 3, 4, 5, 6], 5, [1, 2, 3, 4, 5, 6]));
        list.Add(TopStrip("servo", [1, 2, 3], 10, [1, 2, 3]));
        list.Add(TopStrip("5li-3", [1, 2, 3, 4, 5], 5, [3]));
        list.Add(TopStrip("tekli", [1], 5, [1]));
        list.Add(TopStrip("saha-10", Enumerable.Range(1, 10).ToArray(), 10, Enumerable.Range(1, 10).ToArray()));
        list.Add(TopStrip("valf-4", [1, 2, 3, 4], 20, [1, 2, 3, 4]));
        list.Add(TopStrip("3lu-2", [1, 2, 3], 10, [2]));
        return list;
    }

    private static string[] Bits(string letter, int firstByte) =>
        Enumerable.Range(0, 16).Select(i => $"{letter}{firstByte + i / 8}.{i % 8}").ToArray();

    private static SymbolDefinition Def(string id, string name, string prefix, SymbolRole role, ReferencePosition position,
        List<SymbolPrimitive> primitives, List<ConnectionPoint> pins, List<InternalConnection>? internals = null, string description = "") => new()
        {
            Id = Prefix + id,
            Name = name,
            Category = Category,
            Description = description,
            ReferencePrefix = prefix,
            Role = role,
            ReferencePosition = position,
            Primitives = primitives,
            ConnectionPoints = pins,
            InternalConnections = internals ?? [],
        };

    private static ConnectionPoint Pin(string id, double x, double y, PinDirection d, PinFunction f = PinFunction.General) => new(id, x, y, d, f);
    private static LinePrimitive L(double x1, double y1, double x2, double y2) => new(x1, y1, x2, y2);
    private static RectPrimitive R(double x, double y, double w, double h, bool dashed = false) => new(x, y, w, h) { Dashed = dashed };
    private static CirclePrimitive C(double x, double y, double r) => new(x, y, r);
    private static TextPrimitive T(double x, double y, string text, double h, bool left = false) => new(x, y, text, h) { LeftAligned = left };
    private static InternalConnection Fixed(string a, string b) => new(a, b, InternalConnectionKind.Permanent);

    /// <summary>Kutunun kenarındaki pinlere EPLAN'daki gibi küçük daire.</summary>
    private static IEnumerable<SymbolPrimitive> PinCircles(IEnumerable<ConnectionPoint> pins) => pins.Select(p => C(p.X, p.Y, PinCircle));

    /// <summary>-U1 besleme girişi: üstte L1 L2 L3 N PE, altta saha tarafı klemens numaraları.</summary>
    private static SymbolDefinition Supply()
    {
        string[] top = ["L1", "L2", "L3", "N", "PE"];
        double[] xs = [0, 10, 20, 30, 50];
        var pins = new List<ConnectionPoint>();
        var internals = new List<InternalConnection>();
        for (var i = 0; i < top.Length; i++)
        {
            var f = top[i] switch { "N" => PinFunction.Neutral, "PE" => PinFunction.ProtectiveEarth, _ => PinFunction.Power };
            pins.Add(Pin(top[i], xs[i], 0, PinDirection.Up, f));
            pins.Add(Pin((i + 1).ToString(CultureInfo.InvariantCulture), xs[i], 10, PinDirection.Down, f));
            internals.Add(Fixed(top[i], (i + 1).ToString(CultureInfo.InvariantCulture)));
        }
        var prims = new List<SymbolPrimitive> { R(-5, 0, 60, 10) };
        prims.AddRange(PinCircles(pins));
        return Def("besleme", "Besleme girişi", "U", SymbolRole.Standalone, ReferencePosition.Left, prims, pins, internals);
    }

    /// <summary>-D1 Entes güç analizörü: üstte ölçüm gerilimleri ve besleme, altta akım trafosu girişleri (k/l).</summary>
    private static SymbolDefinition Analyzer()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("N", 0, 0, PinDirection.Up, PinFunction.Neutral), Pin("L1", 10, 0, PinDirection.Up, PinFunction.Power),
            Pin("L2", 20, 0, PinDirection.Up, PinFunction.Power), Pin("L3", 30, 0, PinDirection.Up, PinFunction.Power),
            Pin("A2", 45, 0, PinDirection.Up, PinFunction.Power), Pin("A1", 55, 0, PinDirection.Up, PinFunction.Power),
            Pin("k1", 0, 35, PinDirection.Down, PinFunction.Input), Pin("l1", 10, 35, PinDirection.Down, PinFunction.Input),
            Pin("k2", 20, 35, PinDirection.Down, PinFunction.Input), Pin("l2", 30, 35, PinDirection.Down, PinFunction.Input),
            Pin("k3", 45, 35, PinDirection.Down, PinFunction.Input), Pin("l3", 55, 35, PinDirection.Down, PinFunction.Input),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(-40, 0, 105, 35), R(-35, 6, 24, 23),
            T(-23, 12, "235.1", 3.2), T(-23, 18, "234.6", 3.2), T(-23, 24, "229.1", 3.2),
        };
        prims.AddRange(PinCircles(pins));
        return Def("analizor", "Güç analizörü (3 faz, CT girişli)", "P", SymbolRole.Standalone, ReferencePosition.Right, prims, pins);
    }

    /// <summary>-DK1 faz sırası rölesi: L1 L2 L3 girişleri, NO / COM / NC kontak.</summary>
    private static SymbolDefinition PhaseRelay()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("L1", 0, 0, PinDirection.Up, PinFunction.Power), Pin("L2", 10, 0, PinDirection.Up, PinFunction.Power),
            Pin("L3", 20, 0, PinDirection.Up, PinFunction.Power),
            Pin("NO", 0, 35, PinDirection.Down, PinFunction.ContactNo), Pin("COM", 10, 35, PinDirection.Down, PinFunction.ContactCommon),
            Pin("NC", 20, 35, PinDirection.Down, PinFunction.ContactNc),
        };
        var prims = new List<SymbolPrimitive> { R(-25, 0, 55, 35), R(-20, 7, 12, 21), T(-14, 14, "L1L2L3", 1.8), T(-14, 19, "FAZ", 2.2) };
        prims.AddRange(PinCircles(pins));
        return Def("faz-rolesi", "Faz sırası rölesi (NO/COM/NC)", "K", SymbolRole.Standalone, ReferencePosition.Right, prims, pins,
            [new("COM", "NO"), new("COM", "NC")]);
    }

    /// <summary>Akım trafosu: primer hat üzerinden geçer, sekonder (S1/S2) sağa çıkar.</summary>
    private static SymbolDefinition CurrentTransformer()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("P1", 0, -10, PinDirection.Up, PinFunction.Power), Pin("P2", 0, 10, PinDirection.Down, PinFunction.Power),
            Pin("S1", 5, -5, PinDirection.Right, PinFunction.Output), Pin("S2", 5, 5, PinDirection.Right, PinFunction.Output),
        };
        var prims = new List<SymbolPrimitive>
        {
            L(0, -10, 0, 10), C(0, 0, 2.5), new PolygonPrimitive([5, -5, 3.5, -5, 3.5, 5, 5, 5]) { Open = true },
        };
        return Def("akim-trafosu", "Akım trafosu (sekonder sağda)", "T", SymbolRole.Standalone, ReferencePosition.Left, prims, pins,
            [Fixed("P1", "P2")]);
    }

    /// <summary>AC/DC güç kaynağı: üstte L N, altta + −.</summary>
    private static SymbolDefinition PowerSupply()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("L", 0, 0, PinDirection.Up, PinFunction.Power), Pin("N", 15, 0, PinDirection.Up, PinFunction.Neutral),
            Pin("+", 0, 20, PinDirection.Down, PinFunction.SupplyPositive), Pin("-", 15, 20, PinDirection.Down, PinFunction.SupplyNegative),
        };
        var prims = new List<SymbolPrimitive> { R(-5, 0, 25, 20), L(-5, 20, 20, 0), T(1, 6, "~", 4), T(14, 15, "=", 4) };
        return Def("guc-kaynagi", "Güç kaynağı AC/DC", "G", SymbolRole.Standalone, ReferencePosition.Left, prims, pins);
    }

    /// <summary>4 portlu endüstriyel Ethernet switch.</summary>
    private static SymbolDefinition EthernetSwitch()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("DC+", 0, 0, PinDirection.Up, PinFunction.SupplyPositive), Pin("DC-", 10, 0, PinDirection.Up, PinFunction.SupplyNegative),
        };
        for (var i = 1; i <= 4; i++)
        {
            pins.Add(Pin(i.ToString(CultureInfo.InvariantCulture), 40, 5 + 10 * i, PinDirection.Right, PinFunction.Communication));
        }
        var prims = new List<SymbolPrimitive> { R(-35, 0, 75, 60), R(-30, 6, 26, 48) };
        for (var i = 1; i <= 4; i++)
        {
            prims.Add(R(-24, 5 + 10 * i - 3, 8, 6));
        }
        prims.AddRange(PinCircles(pins));
        return Def("eth-switch", "Ethernet switch, 4 port", "A", SymbolRole.Standalone, ReferencePosition.Left, prims, pins);
    }

    private static SymbolDefinition PanelPc()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("1", 0, 0, PinDirection.Left, PinFunction.SupplyPositive), Pin("2", 0, 5, PinDirection.Left, PinFunction.SupplyNegative),
            Pin("ETH", 0, 65, PinDirection.Left, PinFunction.Communication),
        };
        var prims = new List<SymbolPrimitive> { R(0, -40, 195, 155), R(15, -30, 170, 125) };
        prims.AddRange(PinCircles(pins));
        return Def("panel-pc", "Panel PC", "A", SymbolRole.Standalone, ReferencePosition.Right, prims, pins);
    }

    /// <summary>Işıklı buton: üstte kontak (NO1/NO2 veya NC1/NC2), altta lamba (L1/L2).</summary>
    private static SymbolDefinition LitButton(bool nc)
    {
        var (a, b) = nc ? ("NC1", "NC2") : ("NO1", "NO2");
        var f = nc ? PinFunction.ContactNc : PinFunction.ContactNo;
        var pins = new List<ConnectionPoint>
        {
            Pin(a, 0, 0, PinDirection.Up, f), Pin(b, 15, 0, PinDirection.Up, f),
            Pin("L1", 0, 25, PinDirection.Down), Pin("L2", 15, 25, PinDirection.Down),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(-5, 0, 25, 25), L(0, 0, 0, 7), L(15, 0, 15, 7),
            nc ? L(0, 7, 15, 7) : L(0, 7, 12, 3.5),
            L(7.5, 3, 7.5, 6), // buton itme çubuğu
            L(0, 25, 0, 18), L(0, 18, 5, 18), L(10, 18, 15, 18), L(15, 18, 15, 25), C(7.5, 18, 2.5),
            L(5.7, 16.2, 9.3, 19.8), L(5.7, 19.8, 9.3, 16.2),
        };
        if (nc)
        {
            prims.Add(L(12, 4, 15, 7));
        }
        prims.AddRange(PinCircles(pins));
        return Def(nc ? "buton-isikli-nc" : "buton-isikli-no", nc ? "Işıklı buton (NC + lamba)" : "Işıklı buton (NO + lamba)", "S",
            SymbolRole.Standalone, ReferencePosition.Left, prims, pins, [new(a, b)]);
    }

    private static SymbolDefinition EmergencyStop()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("11", 0, 0, PinDirection.Up, PinFunction.ContactNc), Pin("12", 15, 0, PinDirection.Up, PinFunction.ContactNc),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(-5, 0, 25, 25), L(0, 0, 0, 5), L(15, 0, 15, 5), L(0, 5, 15, 5), L(12, 2, 15, 5),
            C(7.5, 15, 7), T(7.5, 13.5, "ACİL", 2.2), T(7.5, 17.5, "STOP", 2.2),
        };
        prims.AddRange(PinCircles(pins));
        return Def("acil-stop", "Acil stop butonu (NC)", "S", SymbolRole.Standalone, ReferencePosition.Left, prims, pins, [new("11", "12")]);
    }

    /// <summary>Üç renkli ışıklı ikaz kulesi: kırmızı / sarı / yeşil ve ortak GND.</summary>
    private static SymbolDefinition StackLight()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("RED", 20, 10, PinDirection.Right), Pin("YLW", 20, 30, PinDirection.Right), Pin("GRN", 20, 50, PinDirection.Right),
            Pin("GND", 10, 65, PinDirection.Down, PinFunction.SupplyNegative),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(0, 0, 20, 65), L(0, 20, 20, 20), L(0, 40, 20, 40), L(0, 60, 20, 60),
            T(9, 10, "KIRMIZI", 2), T(9, 30, "SARI", 2), T(9, 50, "YEŞİL", 2),
        };
        prims.AddRange(PinCircles(pins));
        return Def("isikli-ikaz", "Işıklı ikaz kulesi (3 renk)", "P", SymbolRole.Standalone, ReferencePosition.Left, prims, pins);
    }

    /// <summary>Frekans inverteri: üstte şebeke, altta motor, sağda kumanda (DI1..DI3, DQ1).</summary>
    private static SymbolDefinition Drive()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("L1", 0, 0, PinDirection.Up, PinFunction.Power), Pin("L2", 10, 0, PinDirection.Up, PinFunction.Power),
            Pin("L3", 20, 0, PinDirection.Up, PinFunction.Power),
            Pin("U", 0, 90, PinDirection.Down, PinFunction.Power), Pin("V", 10, 90, PinDirection.Down, PinFunction.Power),
            Pin("W", 20, 90, PinDirection.Down, PinFunction.Power),
            Pin("DI1", 35, 20, PinDirection.Right, PinFunction.Input), Pin("DI2", 35, 35, PinDirection.Right, PinFunction.Input),
            Pin("DI3", 35, 50, PinDirection.Right, PinFunction.Input), Pin("DQ1", 35, 80, PinDirection.Right, PinFunction.Output),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(-35, 0, 70, 90), R(-29, 10, 24, 50), R(-26, 14, 18, 8), T(-17, 18, "50.00", 2.5),
            C(-21, 30, 2), C(-13, 30, 2), C(-21, 38, 2), C(-13, 38, 2), C(-17, 50, 4),
            L(-25, 70, -5, 70), T(-15, 75, "~ / ~", 3),
        };
        prims.AddRange(PinCircles(pins));
        return Def("surucu", "Frekans inverteri (kumanda girişli)", "T", SymbolRole.Standalone, ReferencePosition.Left, prims, pins);
    }

    /// <summary>Siemens S7-1200 CPU 1214C DC/DC/DC: X10/X11 besleme ve ortaklar, X12 çıkış beslemesi, PROFINET.</summary>
    private static SymbolDefinition Cpu()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("L+", 0, 0, PinDirection.Up, PinFunction.SupplyPositive), Pin("M", 5, 0, PinDirection.Up, PinFunction.SupplyNegative),
            Pin("PE", 10, 0, PinDirection.Up, PinFunction.ProtectiveEarth), Pin("L+2", 15, 0, PinDirection.Up, PinFunction.Output),
            Pin("M2", 20, 0, PinDirection.Up, PinFunction.Output), Pin("1M", 25, 0, PinDirection.Up, PinFunction.Input),
            Pin("2M", 45, 0, PinDirection.Up, PinFunction.SupplyNegative),
            Pin("3L+", 0, 100, PinDirection.Down, PinFunction.SupplyPositive), Pin("3M", 5, 100, PinDirection.Down, PinFunction.SupplyNegative),
            Pin("ETH", 55, 80, PinDirection.Right, PinFunction.Communication),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(-10, 0, 65, 100, dashed: true), R(-6, 8, 57, 86),
            T(-4, 5, "X10", 2, left: true), T(36, 5, "X11", 2, left: true), T(-4, 97, "X12", 2, left: true),
            T(22.5, 18, "{Reference}", 4),
            T(22.5, 30, "CPU 1214C", 3.5), T(22.5, 35, "DC/DC/DC", 2.8), T(22.5, 41, "6ES7 214-1AG40-0XB0", 1.8),
            T(22.5, 52, "24VDC INPUTS  DI a / DI b", 1.6), T(22.5, 56, "ANALOG INPUTS AI 0..1", 1.6),
            T(22.5, 66, "24VDC OUTPUTS  DQ a / DQ b", 1.6), T(36, 80, "PROFINET", 1.8),
        };
        prims.AddRange(PinCircles(pins));
        return Def("s7-1200", "Siemens S7-1200 CPU 1214C DC/DC/DC", "A", SymbolRole.Standalone, ReferencePosition.InSymbol, prims, pins);
    }

    /// <summary>Beijer GN-9287 haberleşme adaptörü (G serisi uzak G/Ç kafası).</summary>
    private static SymbolDefinition Coupler()
    {
        var pins = new List<ConnectionPoint>
        {
            Pin("ETH2", 10, 0, PinDirection.Up, PinFunction.Communication), Pin("ETH1", 0, 85, PinDirection.Left, PinFunction.Communication),
            Pin("S+", 5, 115, PinDirection.Down, PinFunction.SupplyPositive), Pin("S-", 10, 115, PinDirection.Down, PinFunction.SupplyNegative),
        };
        var prims = new List<SymbolPrimitive>
        {
            R(0, 0, 12.5, 115, dashed: true), T(6.25, 8, "{Reference}", 2.2), T(6.25, 12, "GN-9287", 1.6), T(6.25, 16, "Beijer", 1.8),
        };
        prims.AddRange(PinCircles(pins));
        return Def("gn-9287", "Beijer GN-9287 haberleşme adaptörü", "A", SymbolRole.Standalone, ReferencePosition.InSymbol, prims, pins);
    }

    /// <summary>Beijer G serisi G/Ç modülü: sağ kenarda kanallar (adres yazılı), altta GND.</summary>
    private static SymbolDefinition Module(string reference, string type, string kind, string[] addresses, bool input)
    {
        var pins = new List<ConnectionPoint>();
        var prims = new List<SymbolPrimitive>
        {
            R(0, 0, 12.5, 115, dashed: true), T(6.25, 8, "{Reference}", 2.2), T(6.25, 12, type, 1.6), T(6.25, 16, "Beijer", 1.8),
        };
        var step = addresses.Length > 4 ? 5.0 : 10.0;
        for (var i = 0; i < addresses.Length; i++)
        {
            var y = 25 + step * i;
            pins.Add(Pin((i + 1).ToString(CultureInfo.InvariantCulture), 12.5, y, PinDirection.Right, input ? PinFunction.Input : PinFunction.Output));
            prims.Add(T(5.5, y, $"{kind}:{addresses[i]}", 1.4));
        }
        pins.Add(Pin("GND", 5, 115, PinDirection.Down, PinFunction.SupplyNegative));
        prims.AddRange(PinCircles(pins));
        var id = ModuleId(reference)[Prefix.Length..];
        return Def(id, $"Beijer {type} {kind} modülü ({reference})", "A", SymbolRole.Standalone, ReferencePosition.InSymbol, prims, pins);
    }

    /// <summary>
    /// EPLAN tarzı PLC kanal kutusu: içinde işlev metni, adres ve etiket + çapraz referans. side: pinin bulunduğu
    /// kenar (T üst, B alt, L sol, R sağ). Ekleme noktası pindir.
    /// </summary>
    private static SymbolDefinition PlcBox(bool input, char side)
    {
        const double w = 28, h = 15, lead = 5;
        var (dir, bx, by, lx2, ly2) = side switch
        {
            'T' => (PinDirection.Up, -w / 2, lead, 0.0, lead),
            'B' => (PinDirection.Down, -w / 2, -lead - h, 0.0, -lead),
            'L' => (PinDirection.Left, lead, -h / 2, lead, 0.0),
            _ => (PinDirection.Right, -lead - w, -h / 2, -lead, 0.0),
        };
        var cx = bx + w / 2;
        var prims = new List<SymbolPrimitive>
        {
            L(0, 0, lx2, ly2), R(bx, by, w, h), L(bx, by + 5, bx + w, by + 5), L(bx, by + 10, bx + w, by + 10),
            T(cx, by + 2.5, "{Description}", 1.5), T(cx, by + 7.5, "{Pin:1}", 2.2), T(cx, by + 12.5, "{Reference} {CrossReference}", 1.5),
        };
        var pins = new List<ConnectionPoint> { Pin("1", 0, 0, dir, input ? PinFunction.Input : PinFunction.Output) };
        var what = input ? "giriş" : "çıkış";
        return Def($"plc-{(input ? "giris" : "cikis")}-{char.ToLowerInvariant(side)}", $"PLC {what} kanalı (EPLAN kutusu, pin {side})", "A",
            SymbolRole.Contact, ReferencePosition.InSymbol, prims, pins);
    }

    /// <summary>Röle bobini (etiket solda, EPLAN gibi).</summary>
    private static SymbolDefinition Coil() => Def("bobin", "Röle bobini", "K", SymbolRole.Main, ReferencePosition.Left,
        [R(-5, -3, 10, 6), L(0, -10, 0, -3), L(0, 3, 0, 10)],
        [Pin("A1", 0, -10, PinDirection.Up, PinFunction.Coil), Pin("A2", 0, 10, PinDirection.Down, PinFunction.Coil)]);

    private static SymbolDefinition ContactNo(ReferencePosition position, string id) => Def(id, "Röle kontağı (NO)", "K", SymbolRole.Contact, position,
        [L(0, -10, 0, -4), L(0, 10, 0, 4), L(0, 4, -3.5, -4.5)],
        [Pin("13", 0, -10, PinDirection.Up, PinFunction.ContactNo), Pin("14", 0, 10, PinDirection.Down, PinFunction.ContactNo)],
        [new("13", "14")]);

    /// <summary>4 kutuplu otomatik sigorta (etiket solda).</summary>
    private static SymbolDefinition Mcb4p()
    {
        var prims = new List<SymbolPrimitive>();
        var pins = new List<ConnectionPoint>();
        var internals = new List<InternalConnection>();
        for (var i = 0; i < 4; i++)
        {
            var x = -15 + 10 * i;
            prims.Add(L(x, -10, x, -4));
            prims.Add(L(x, 10, x, 4));
            prims.Add(L(x, 4, x - 3.5, -4.5));
            prims.Add(L(x - 1, -5, x + 1, -3));
            prims.Add(L(x + 1, -5, x - 1, -3));
            var a = (2 * i + 1).ToString(CultureInfo.InvariantCulture);
            var b = (2 * i + 2).ToString(CultureInfo.InvariantCulture);
            pins.Add(Pin(a, x, -10, PinDirection.Up, PinFunction.Power));
            pins.Add(Pin(b, x, 10, PinDirection.Down, PinFunction.Power));
            internals.Add(new(a, b));
        }
        prims.Add(new LinePrimitive(-22, 0, 13, 0) { Dashed = true });
        prims.Add(R(-25, -1.5, 3, 3));
        return Def("sigorta-4p", "Otomatik sigorta 4 kutup", "Q", SymbolRole.Standalone, ReferencePosition.Left, prims, pins, internals);
    }

    /// <summary>Dikey klemens şeridi, köprülü (24 V / 0 V dağıtımı): solda besleme girişleri, sağda çıkışlar.</summary>
    private static SymbolDefinition BridgedStrip(string name, int count, int[] left, int[] right)
    {
        var prims = new List<SymbolPrimitive> { L(-2.5, 0, -2.5, 5 * (count - 1)) };
        var pins = new List<ConnectionPoint>();
        for (var i = 1; i <= count; i++)
        {
            var y = 5.0 * (i - 1);
            prims.Add(C(0, y, 1));
            prims.Add(L(-2.5, y, -1, y));
            if (left.Contains(i))
            {
                prims.Add(L(-5, y, -2.5, y));
                pins.Add(Pin("L" + i, -5, y, PinDirection.Left, PinFunction.Terminal));
            }
            if (right.Contains(i))
            {
                prims.Add(L(1, y, 5, y));
                pins.Add(Pin("R" + i, 5, y, PinDirection.Right, PinFunction.Terminal));
            }
            else
            {
                prims.Add(T(1.8, y - 2.2, i.ToString(CultureInfo.InvariantCulture), 2, left: true));
            }
        }
        // Köprülü şerit: bütün uçlar tek iletken
        var internals = pins.Skip(1).Select(p => Fixed(pins[0].Id, p.Id)).ToList();
        return Def("klemens-" + name.ToLowerInvariant(), $"Klemens şeridi {name} ({count} klemens, köprülü)", "X", SymbolRole.Standalone,
            ReferencePosition.Left, prims, pins, internals);
    }

    /// <summary>Saha klemens şeridi: kablolar üstten girer (saha tarafı çizilmez). Kullanılmayan klemensler yalnız çizilir.</summary>
    private static SymbolDefinition TopStrip(string name, int[] numbers, double pitch, int[] used)
    {
        var prims = new List<SymbolPrimitive>();
        var pins = new List<ConnectionPoint>();
        for (var i = 0; i < numbers.Length; i++)
        {
            var x = pitch * i;
            var n = numbers[i].ToString(CultureInfo.InvariantCulture);
            prims.Add(C(x, 1, 1));
            if (used.Contains(numbers[i]))
            {
                pins.Add(Pin(n, x, 0, PinDirection.Up, PinFunction.Terminal));
            }
            else
            {
                prims.Add(T(x + 1.5, 1, n, 2, left: true));
            }
        }
        return Def("klemens-" + name, $"Saha klemens şeridi ({numbers.Length} klemens)", "X", SymbolRole.Standalone, ReferencePosition.Left, prims, pins);
    }

    /// <summary>Geçişli klemens şeridi (sürücü → motor): üstte giriş, altta çıkış.</summary>
    private static SymbolDefinition ThroughStrip(string name, int count, double pitch)
    {
        var prims = new List<SymbolPrimitive>();
        var pins = new List<ConnectionPoint>();
        var internals = new List<InternalConnection>();
        for (var i = 1; i <= count; i++)
        {
            var x = pitch * (i - 1);
            var n = i.ToString(CultureInfo.InvariantCulture);
            prims.Add(L(x, 0, x, 4));
            prims.Add(C(x, 5, 1));
            prims.Add(L(x, 6, x, 10));
            pins.Add(Pin(n, x, 0, PinDirection.Up, PinFunction.Terminal));
            pins.Add(Pin(n + "'", x, 10, PinDirection.Down, PinFunction.Terminal));
            internals.Add(Fixed(n, n + "'"));
        }
        return Def("klemens-" + name, $"Klemens şeridi ({count} klemens, geçişli)", "X", SymbolRole.Standalone, ReferencePosition.Left, prims, pins, internals);
    }
}
