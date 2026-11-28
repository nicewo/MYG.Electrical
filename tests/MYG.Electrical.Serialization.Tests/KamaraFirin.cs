using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Symbols;
using static MYG.Electrical.Serialization.Tests.KamaraFirinSymbols;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>
/// Örnek proje "Kamara Fırın Projesi" (Data Otomasyon, proje no 002): EPLAN'da çizilmiş 17 sayfalık gaz brülörlü
/// kamara fırını panosunun MYG Electrical ile yeniden çizimi. Sayfa yapısı, cihaz etiketleri, PLC adresleri, işlev
/// metinleri ve sayfa bağlantıları özgün projeden alınmıştır; yerleşim MYG'nin 5 mm grid'ine ve otomatik kablolama
/// kurallarına (hizalı pinler, dirsek/T bağlantı noktaları) göre yeniden yapılmıştır.
/// </summary>
public sealed class KamaraFirin
{
    public const string FileName = "KamaraFirin.myge";

    public SymbolCatalog Catalog { get; }
    public EditorSession Session { get; }
    public ProjectDocument Doc => Session.Document;

    public KamaraFirin()
    {
        var doc = ProjectDocument.CreateNew("Kamara Fırın Projesi");
        doc.Project = new ProjectInfo
        {
            Name = "Kamara Fırın Projesi",
            MachineName = "Kamara Fırın Tübitak",
            Company = "Data Otomasyon",
            Customer = "Data Otomasyon",
            ProjectNumber = "002",
            PreparedBy = "MYUSU",
            Date = "02.04.2025",
            Revision = string.Empty,
        };
        doc.Symbols = All();
        Catalog = BuiltInSymbolLibrary.CreateCatalog(null);
        ProjectSymbols.Register(doc, Catalog);
        Session = new EditorSession(doc, Catalog);

        Session.ActivePage = doc.Pages[1];
        doc.Pages[1].Name = "Ana Besleme";
        MainSupply();
        Distribution();
        Supply24V();
        Cabinet();
        PanelPc();
        PlcSupply();
        Drives();
        BurnerIgnition();
        BurnerGroup(first: true);
        BurnerGroup(first: false);
        ServoPower();
        Servos();
        AnalogInputs();
        DigitalInputs();
        Valves();
        FieldValves();
        Records();

        Session.NumberWires(renumberAll: true);
        ProjectSymbols.Embed(doc, Catalog);
    }

    // ───────────── yardımcılar ─────────────

    private void NewPage(string name)
    {
        var page = Session.AddPage(PageType.Schematic, name);
        Session.ActivePage = page;
    }

    private DeviceObject Put(string symbolId, double x, double y, string reference, double rotation = 0, string description = "",
        Dictionary<string, string>? pins = null)
    {
        var def = Catalog.Find(symbolId) ?? throw new InvalidOperationException(symbolId);
        var d = def.CreateDevice(x, y, reference);
        d.Rotation = rotation;
        d.Description = description;
        Session.AddPlacements([d]);
        if (pins is not null)
        {
            d.PinDesignations = pins;
        }
        return d;
    }

    private DeviceObject K(string id, double x, double y, string reference, double rotation = 0, string description = "",
        Dictionary<string, string>? pins = null) => Put(Prefix + id, x, y, reference, rotation, description, pins);

    /// <summary>EPLAN tarzı PLC kanal kutusu; (x, y) pinin yeri, side pinin bulunduğu kenar.</summary>
    private void Io(bool input, char side, double x, double y, string reference, string address, string description) =>
        Put(PlcBoxId(input, side), x, y, reference, 0, description, new() { ["1"] = address });

    /// <summary>Bağlantı noktası: kolları R/L/U/D harfleriyle (2 kol dirsek, 3 kol T, 4 kol çapraz).</summary>
    private void J(double x, double y, string arms)
    {
        var want = arms.Select(c => c switch
        {
            'R' => PinDirection.Right,
            'L' => PinDirection.Left,
            'U' => PinDirection.Up,
            _ => PinDirection.Down,
        }).ToHashSet();
        var shape = want.Count switch { 2 => JunctionShape.Angle, 3 => JunctionShape.Tee, _ => JunctionShape.Cross };
        foreach (var rotation in new[] { 0.0, 90, 180, 270 })
        {
            var j = new JunctionObject { X = x, Y = y, Shape = shape, Rotation = rotation };
            if (j.GetGlobalPins().Select(p => p.Direction).ToHashSet().SetEquals(want))
            {
                Session.AddObject(j, select: false);
                return;
            }
        }
        throw new InvalidOperationException($"Bağlantı noktası kurulamadı: {arms} ({x}, {y})");
    }

    private void Pot(string name, PotentialKind kind, double x, double y, double rotation = 0, string voltage = "") =>
        Session.AddObject(new PotentialObject { X = x, Y = y, Rotation = rotation, Name = name, PotentialKind = kind, Voltage = voltage }, select: false);

    /// <summary>Sayfa bağlantısı. Sol kenarda gelen (kablo sağa): 180; sağ kenarda giden (kablo soldan): 0.</summary>
    private void Link(string name, double x, double y, double rotation) =>
        Session.AddObject(new PageLinkObject { X = x, Y = y, Rotation = rotation, Name = name }, select: false);

    private void In(string name, double y, double x = 55) => Link(name, x, y, 180);
    private void Out(string name, double y, double x = 375) => Link(name, x, y, 0);

    private static Dictionary<string, string> Numbers(params (string Pin, string Label)[] map) => map.ToDictionary(m => m.Pin, m => m.Label);

    /// <summary>Köprülü şeridin L/R pinlerine klemens numarası.</summary>
    private static Dictionary<string, string> StripNumbers(IEnumerable<int> left, IEnumerable<int> right) =>
        left.Select(i => ("L" + i, i.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Concat(right.Select(i => ("R" + i, i.ToString(System.Globalization.CultureInfo.InvariantCulture))))
            .ToDictionary(x => x.Item1, x => x.Item2);

    // ───────────── sayfalar ─────────────

    /// <summary>Sayfa 2: besleme girişi, 4P ana sigorta, akım trafoları, güç analizörü, faz sırası rölesi.</summary>
    private void MainSupply()
    {
        K("besleme", 60, 225, "-U1", description: "Besleme Giriş");
        Pot("L1", PotentialKind.L1, 60, 245, 180, "400VAC");
        Pot("L2", PotentialKind.L2, 70, 245, 180, "400VAC");
        Pot("L3", PotentialKind.L3, 80, 245, 180, "400VAC");
        Pot("N", PotentialKind.N, 90, 245, 180, "230VAC");
        Pot("PE", PotentialKind.PE, 110, 245, 180);
        K("sigorta-4p", 75, 210, "-Q1");
        Put("iec.terminal.pe", 110, 215, "-PE");
        Pot("PE", PotentialKind.PE, 110, 200);

        K("akim-trafosu", 60, 135, "-IT1", description: "L1 AKIM TRAFOSU");
        K("akim-trafosu", 70, 160, "-IT2", description: "L2 AKIM TRAFOSU");
        K("akim-trafosu", 80, 185, "-IT3", description: "L3 AKIM TRAFOSU");

        // Baralar: Q1 → yukarı → sağa, sayfa 3'e
        J(60, 30, "RD");
        J(70, 40, "RD");
        J(80, 50, "RD");
        J(90, 60, "RD");
        K("analizor", 150, 80, "-D1", description: "ENTES GÜÇ ANALİZÖRÜ");
        J(150, 60, "LRD");
        J(195, 60, "LRD");
        J(160, 30, "LRD");
        J(205, 30, "LRD");
        J(170, 40, "LRD");
        J(180, 50, "LRD");
        // Akım trafosu sekonderleri → analizör k/l girişleri
        J(150, 130, "LU");
        J(160, 140, "LU");
        J(170, 155, "LU");
        J(180, 165, "LU");
        J(195, 180, "LU");
        J(205, 190, "LU");

        K("faz-rolesi", 290, 80, "-DK1");
        J(290, 30, "LRD");
        J(300, 40, "LRD");
        J(310, 50, "LRD");
        Out("L1", 30);
        Out("L2", 40);
        Out("L3", 50);
        Out("N", 60);
        // Faz rölesi kontağı: COM'a 24 V, NO / NC PLC girişlerine
        Pot("DC24", PotentialKind.DcPositive, 300, 135, 180, "24VDC");
        J(290, 125, "UL");
        J(275, 125, "RD");
        Io(true, 'T', 275, 140, "-PLC10", "I7.3", "FAZ SIRASI N.O");
        J(310, 125, "UR");
        J(325, 125, "LD");
        Io(true, 'T', 325, 140, "-PLC10", "I7.4", "FAZ SIRASI NC");
    }

    /// <summary>Sayfa 3: invertör sigortaları (F1, F2) ve 1 fazlı dağıtım sigortaları (F3..F9).</summary>
    private void Distribution()
    {
        NewPage("Besleme Dağıtım");
        In("L1", 30);
        In("L2", 40);
        In("L3", 50);
        In("N", 60);
        J(65, 30, "LRD");
        J(125, 30, "LRD");
        J(205, 30, "LD");
        J(135, 40, "LRD");
        J(215, 40, "LD");
        J(145, 50, "LRD");
        J(225, 50, "LD");
        J(60, 60, "LD");
        J(60, 245, "UR");
        Out("N", 245);

        Put("iec.mpcb.3p", 135, 95, "-F1", description: "22KW invertör");
        Put("iec.mpcb.3p", 215, 95, "-F2", description: "2KW invertör");
        J(125, 115, "UR");
        Out("L1.1", 115);
        J(135, 120, "UR");
        Out("L2.1", 120);
        J(145, 125, "UR");
        Out("L3.1", 125);
        J(205, 135, "UR");
        Out("L1.2", 135);
        J(215, 140, "UR");
        Out("L2.2", 140);
        J(225, 145, "UR");
        Out("L3.2", 145);

        // L1'den 1 fazlı dağıtım barası
        J(65, 165, "UR");
        (string Ref, string Text, string Link)[] mcbs =
        [
            ("-F3", "GRUP 1", "L1.9"), ("-F4", "GRUP2", "L1.8"), ("-F5", "24DC", "L1.7"), ("-F6", "PANEL PC", "L1.6"),
            ("-F7", "PRIZ", "L1.5"), ("-F8", "PANO", "L1.4"), ("-F9", "KUMANDA", "L1.3"),
        ];
        for (var i = 0; i < mcbs.Length; i++)
        {
            var x = 115 + 30 * i;
            J(x, 165, i == mcbs.Length - 1 ? "LD" : "LRD");
            Put("iec.mcb.1p", x, 185, mcbs[i].Ref, description: mcbs[i].Text);
            var row = 235 - 5 * i; // iç içe L'ler: soldaki sigorta en alttan çıkar
            J(x, row, "UR");
            Out(mcbs[i].Link, row);
        }
    }

    /// <summary>Sayfa 4: 24 VDC güç kaynağı, + / 0 V dağıtım klemensleri, Ethernet switch.</summary>
    private void Supply24V()
    {
        NewPage("24V Besleme, Ethernet Switch");
        In("L1.7", 30);
        In("N", 40);
        K("guc-kaynagi", 100, 60, "-V1");
        J(100, 30, "LD");
        J(115, 40, "LD");
        J(100, 95, "URD");
        Pot("DC24", PotentialKind.DcPositive, 100, 105, 180, "24VDC");
        J(115, 160, "URD");
        Pot("DC0", PotentialKind.DcNegative, 115, 170, 180, "24VDC");

        int[] x1Right = [1, 2, 3, 4, 6, 8];
        int[] x2Right = [1, 2, 3, 5, 6, 7, 8, 9];
        K("klemens-x1", 230, 95, "-X1", pins: StripNumbers([1, 2], x1Right));
        K("klemens-x2", 230, 160, "-X2", pins: StripNumbers([1, 2], x2Right));
        foreach (var i in x1Right)
        {
            Out($"DC24.{i}", 95 + 5 * (i - 1));
        }
        foreach (var i in x2Right)
        {
            Out($"DC0.{i}", 160 + 5 * (i - 1));
        }

        K("eth-switch", 110, 190, "-ETH");
        J(110, 100, "DR");
        J(120, 165, "DR");
        Out("ETH1", 205, 260);
        Out("ETH2", 215, 260);
    }

    /// <summary>Sayfa 5: pano prizi, aydınlatma, fan; start / stop / acil stop butonları ve ışıklı ikaz.</summary>
    private void Cabinet()
    {
        NewPage("Pano");
        In("L1.4", 40);
        In("L1.5", 50);
        In("N", 60);
        Put("iec.socket", 75, 130, "-PR1");
        J(70, 50, "LD");
        J(80, 60, "LRD");
        Pot("PE", PotentialKind.PE, 75, 100);
        Put("iec.light", 125, 130, "-H1");
        J(120, 40, "LRD");
        J(130, 60, "LRD");
        Put("iec.fan.1ph", 155, 130, "-MF1");
        J(150, 40, "LD");
        J(160, 60, "LD");

        // Butonlar: 24 V üstten kontaklara, kontak çıkışı yukarıdaki PLC girişine, lamba alttaki PLC çıkışından
        In("DC24.3", 175);
        In("DC0.3", 185);
        J(170, 175, "LU");
        J(170, 100, "DR");
        J(200, 100, "LRD");
        J(250, 100, "LRD");
        J(300, 100, "LD");
        K("buton-isikli-no", 200, 125, "-B1");
        K("buton-isikli-nc", 250, 125, "-B2");
        K("acil-stop", 300, 125, "-B3");
        Io(true, 'B', 215, 80, "-PLC1", "I0.0", "START BUTONU");
        Io(true, 'B', 265, 80, "-PLC1", "I0.1", "STOP BUTONU");
        Io(true, 'B', 315, 80, "-PLC1", "I1.5", "ACİL STOP BUTONU");
        Io(false, 'T', 200, 205, "-PLC1", "Q0.0", "START GÖSTERGE");
        Io(false, 'T', 250, 205, "-PLC1", "Q0.1", "STOP GÖSTERGE");
        J(215, 185, "LRU");
        J(265, 185, "LRU");
        J(350, 185, "LU");

        K("isikli-ikaz", 340, 80, "-H2", description: "IŞIKLI İKAZ");
        Io(false, 'L', 370, 90, "-PLC13", "Q4.3", "KIRMIZI İKAZ");
        Io(false, 'L', 370, 110, "-PLC13", "Q4.0", "SARI İKAZ");
        Io(false, 'L', 370, 130, "-PLC13", "Q4.1", "YEŞİL İKAZ");
    }

    /// <summary>Sayfa 6: panel PC ve güç kaynağı.</summary>
    private void PanelPc()
    {
        NewPage("Panel PC");
        In("L1.6", 30);
        In("N", 40);
        In("ETH1", 175);
        K("guc-kaynagi", 85, 70, "-VPC", description: "PANEL PC GÜÇ KAYNAĞI");
        J(85, 30, "LD");
        J(100, 40, "LD");
        J(85, 110, "UR");
        J(100, 115, "UR");
        K("panel-pc", 175, 110, "-PC1", description: "PANEL PC");
    }

    /// <summary>Sayfa 7: S7-1200 CPU, Beijer haberleşme adaptörü ve G/Ç modülleri, beslemeleri.</summary>
    private void PlcSupply()
    {
        NewPage("PLC Besleme");
        In("DC24.1", 30, 50);
        In("DC0.1", 40, 50);
        In("DC24.2", 235, 50);
        In("DC0.2", 245, 50);
        Link("ETH2", 140, 60, 270);

        K("s7-1200", 70, 90, "-PLC1", pins: Numbers(("L+2", "L+"), ("M2", "M")));
        J(60, 30, "LRD");
        J(70, 30, "LD");
        J(60, 200, "UR");
        J(70, 200, "LU");
        J(55, 40, "LRD");
        J(75, 40, "LRD");
        J(115, 40, "LD");
        J(55, 205, "UR");
        J(75, 205, "LU");

        K("gn-9287", 130, 85, "-PLC2");
        J(135, 235, "LU");
        J(140, 245, "LRU");
        for (var k = 1; k <= 12; k++)
        {
            var reference = $"-PLC{k + 2}";
            Put(ModuleId(reference), 130 + 20 * k, 85, reference);
            J(135 + 20 * k, 245, k == 12 ? "LU" : "LRU");
        }
    }

    /// <summary>Sayfa 8: 2,2 kW ve 22 kW frekans inverterleri, motorlar, PLC kumandaları.</summary>
    private void Drives()
    {
        NewPage("Motor Sürücüler");
        In("L1.1", 30);
        In("L2.1", 40);
        In("L3.1", 50);
        In("L1.2", 60);
        In("L2.2", 70);
        In("L3.2", 80);
        var motorTerminals = Numbers(("1'", "1"), ("2'", "2"), ("3'", "3"));

        K("surucu", 80, 120, "-S1", description: "2,2KW ES355");
        J(80, 60, "LD");
        J(90, 70, "LD");
        J(100, 80, "LD");
        K("klemens-motor", 80, 220, "-X3", pins: motorTerminals);
        Put("iec.motor.3ph", 90, 245, "-M1");
        Io(false, 'L', 125, 140, "-PLC13", "Q4.5", "MOTOR 2 İLERİ");
        Io(false, 'L', 125, 155, "-PLC13", "Q4.6", "MOTOR 2 GERİ");
        J(120, 170, "LRD");
        Io(false, 'L', 125, 170, "-PLC13", "Q4.4", "INVERTER RESET");
        Link("INVERTER RESET", 120, 180, 90);
        Io(true, 'L', 125, 200, "-PLC10", "I6.1", "BACA İNVERTER ARIZA");

        K("surucu", 290, 120, "-S2", description: "22KW ES580");
        J(290, 30, "LD");
        J(300, 40, "LD");
        J(310, 50, "LD");
        K("klemens-motor", 290, 220, "-X4", pins: motorTerminals);
        Put("iec.motor.3ph", 300, 245, "-M2");
        Io(false, 'L', 335, 140, "-PLC13", "Q4.7", "MOTOR 1 START");
        Link("INVERTER RESET", 335, 170, 0); // aynı çıkış (Q4.4) iki sürücünün reset girişine
        Io(true, 'L', 335, 200, "-PLC10", "I6.0", "YAKMA İNVERTER ARIZA");
    }

    /// <summary>Sayfa 9: brülör ateşleme röleleri (K2..K13) ve brülör reset rölesi (K17).</summary>
    private void BurnerIgnition()
    {
        NewPage("Brülör Ateşleme");
        In("DC0.5", 30, 45);
        J(50, 30, "LRD");
        // Üst sıra: brülör 1..6 + reset, alt sıra: brülör 7..12 (her sıra kendi 0 V barasından)
        (string Ref, string Q, string Text)[] top =
        [
            ("-K2", "Q2.0", "BRÜLÖR 1 ATEŞLE"), ("-K3", "Q2.1", "BRÜLÖR 2 ATEŞLE"), ("-K4", "Q2.2", "BRÜLÖR 3 ATEŞLE"),
            ("-K5", "Q2.3", "BRÜLÖR 4 ATEŞLE"), ("-K6", "Q2.4", "BRÜLÖR 5 ATEŞLE"), ("-K7", "Q2.5", "BRÜLÖR 6 ATEŞLE"),
            ("-K17", "Q3.5", "BRÜLÖR RESET"),
        ];
        (string Ref, string Q, string Text)[] bottom =
        [
            ("-K8", "Q2.6", "BRÜLÖR 7 ATEŞLE"), ("-K9", "Q2.7", "BRÜLÖR 8 ATEŞLE"), ("-K10", "Q3.0", "BRÜLÖR 9 ATEŞLE"),
            ("-K11", "Q3.1", "BRÜLÖR 10 ATEŞLE"), ("-K12", "Q3.2", "BRÜLÖR 11 ATEŞLE"), ("-K13", "Q3.3", "BRÜLÖR 12 ATEŞLE"),
        ];
        Row(top, 30, 70, 95, last: "LD");
        J(50, 125, "UR");
        Row(bottom, 125, 150, 175, last: "LD");

        void Row((string Ref, string Q, string Text)[] relays, double busY, double coilY, double boxY, string last)
        {
            for (var i = 0; i < relays.Length; i++)
            {
                var x = 85 + 50 * i;
                J(x, busY, i == relays.Length - 1 ? last : "LRD");
                K("bobin", x, coilY, relays[i].Ref, rotation: 180);
                Io(false, 'T', x, boxY, "-PLC12", relays[i].Q, relays[i].Text);
            }
        }
    }

    /// <summary>Sayfa 10 / 11: brülör grupları — ateşleme kontağı, nötr, reset, 24 V ve brülör yandı / arıza girişleri.</summary>
    private void BurnerGroup(bool first)
    {
        NewPage("Grup 1 Brülör");
        var phase = first ? "L1.9" : "L1.8";
        In(phase, 30);
        In("N", 40);
        In("DC24.4", 50);
        // Brülör reset: K17 kontağı DC24.4'ten reset barasına
        J(65, 50, "LRD");
        K("kontak-no", 65, 80, "-K17", description: "BRÜLÖR RESET");
        J(65, 100, "UR");

        var n0 = first ? 1 : 7;
        for (var g = 0; g < 6; g++)
        {
            var n = n0 + g;
            var x0 = 80 + 50 * g;
            var lastGroup = g == 5;
            J(x0, 30, lastGroup ? "LD" : "LRD");
            J(x0 + 5, 40, lastGroup && !first ? "LD" : "LRD");
            J(x0 + 10, 100, lastGroup ? "LD" : "LRD");
            J(x0 + 15, 50, lastGroup && !first ? "LD" : "LRD");
            K("kontak-no", x0, 200, $"-K{n + 1}", rotation: 180);

            var yandi = n <= 8 ? $"I2.{n - 1}" : $"I3.{n - 9}";
            var ariza = n <= 8 ? $"I4.{n - 1}" : $"I5.{n - 9}";
            Io(true, 'B', x0 + 35, 130, "-PLC8", yandi, $"BRÜLÖR {n} YANDI");
            J(x0 + 35, 135, "UL");
            J(x0 + 20, 135, "RD");
            Io(true, 'B', x0 + 35, 170, "-PLC9", ariza, $"BRÜLÖR {n} ARIZA");
            J(x0 + 35, 175, "UL");
            J(x0 + 25, 175, "RD");
            K("klemens-brulor", x0, 240, $"-X{n + 4}");
        }
        if (first)
        {
            Out("N", 40, 385);
            Out("DC24.4", 50, 385);
        }
    }

    /// <summary>Sayfa 12: servo güç röleleri.</summary>
    private void ServoPower()
    {
        NewPage("Servo Güç");
        In("DC0.7", 30);
        (string Ref, double X, string Plc, string Q, string Text)[] relays =
        [
            ("-K14", 75, "-PLC12", "Q3.6", "GRUP 1 SERVO GÜÇ"), ("-K15", 210, "-PLC12", "Q3.7", "GRUP 2 SERVO GÜÇ"),
            ("-K16", 360, "-PLC13", "Q5.3", "TEMİZ HAVA SERVO GÜÇ"),
        ];
        foreach (var r in relays)
        {
            J(r.X, 30, r.Ref == "-K16" ? "LD" : "LRD");
            K("bobin", r.X, 70, r.Ref, rotation: 180, description: r.Text);
            Io(false, 'T', r.X, 140, r.Plc, r.Q, r.Text);
        }
    }

    /// <summary>Sayfa 13: servo valf sürücülerinin beslemesi ve analog set değerleri.</summary>
    private void Servos()
    {
        NewPage("Servolar");
        In("DC24.6", 30);
        In("DC0.6", 40);
        (string Contact, string Plc, string Q, string Text)[] groups =
        [
            ("-K14", "-PLC5", "PQW0", "GRUP 1 GAZ SERVO"), ("-K14", "-PLC5", "PQW2", "GRUP 1 HAVA SERVO"),
            ("-K15", "-PLC5", "PQW4", "GRUP 2 GAZ SERVO"), ("-K15", "-PLC5", "PQW6", "GRUP 2 HAVA SERVO"),
            ("-K16", "-PLC6", "PQW8", "TEMİZ HAVA SERVO"),
        ];
        for (var g = 0; g < groups.Length; g++)
        {
            var x0 = 70 + 65 * g;
            var last = g == groups.Length - 1;
            J(x0, 30, last ? "LD" : "LRD");
            J(x0 + 10, 40, last ? "LD" : "LRD");
            K("kontak-no", x0, 70, groups[g].Contact, rotation: 180);
            Io(false, 'B', x0 + 35, 110, groups[g].Plc, groups[g].Q, groups[g].Text);
            J(x0 + 35, 120, "UL");
            J(x0 + 20, 120, "RD");
            K("klemens-servo", x0, 240, $"-X{17 + g}");
        }
    }

    /// <summary>Sayfa 14 ve 15'in ortak yerleşimi: 5 alt + 5 üst kanal kutusu, 10'lu saha klemensine merdiven kablolama.</summary>
    private void FieldInputs((string Plc, string Address, string Text)[] channels, string strip)
    {
        double[] sources = [110, 140, 170, 200, 230, 260, 290, 320, 350, 380];
        for (var k = 0; k < 10; k++)
        {
            var x = sources[k];
            var lower = k % 2 == 0;
            var pinY = lower ? 130 : 90;
            Io(true, 'B', x, pinY, channels[k].Plc, channels[k].Address, channels[k].Text);
            var terminal = 150 + 10 * k;
            if (Math.Abs(terminal - x) < 0.01)
            {
                continue; // klemensin tam üstünde: düz iner
            }
            // Sağa gidenler (1, 2) dıştan içe, sola gidenler (4..10) iç içe merdiven
            var row = k switch { 0 => 150.0, 1 => 145.0, _ => 155 + 5 * (k - 3) };
            J(x, row, terminal > x ? "UR" : "UL");
            J(terminal, row, terminal > x ? "LD" : "RD");
        }
        K("klemens-saha-10", 150, 240, strip);
    }

    /// <summary>Sayfa 14: saha analog girişleri (transmitterler, lineer cetvel).</summary>
    private void AnalogInputs()
    {
        NewPage("Saha Analog Input");
        In("DC24.8", 30, 50);
        In("DC0.8", 40, 50);
        J(55, 30, "LRD");
        J(95, 40, "LRD");
        Out("DC24.8", 30);
        Out("DC0.8", 40);
        K("klemens-5li-3", 45, 240, "-X22");
        K("klemens-5li-3", 85, 240, "-X23");
        FieldInputs(
        [
            ("-PLC1", "AI 0", "İÇ BASINÇ TRANSMİTTER"), ("-PLC1", "AI 1", "LİNEER CETVEL"),
            ("-PLC3", "PIW0", "1. GRUP GAZ ÖNCESİ TRANSMİTTER"), ("-PLC3", "PIW2", "1. GRUP GAZ SONRASI TRANSMİTTER"),
            ("-PLC3", "PIW4", "1. GRUP HAVA ÖNCESİ TRANSMİTTER"), ("-PLC3", "PIW6", "1. GRUP HAVA SONRASI TRANSMİTTER"),
            ("-PLC4", "PIW8", "2. GRUP GAZ ÖNCESİ TRANSMİTTER"), ("-PLC4", "PIW10", "2. GRUP GAZ SONRASI TRANSMİTTER"),
            ("-PLC4", "PIW12", "2. GRUP HAVA ÖNCESİ TRANSMİTTER"), ("-PLC4", "PIW14", "2. GRUP HAVA SONRASI TRANSMİTTER"),
        ], "-X24");
    }

    /// <summary>Sayfa 15: saha dijital girişleri (basınç şalterleri, gaz tahliye, kapı sensörleri).</summary>
    private void DigitalInputs()
    {
        NewPage("Saha Digital Input");
        In("DC24.8", 30, 50);
        In("DC0.8", 40, 50);
        J(55, 30, "LD");
        J(85, 40, "LD");
        K("klemens-tekli", 55, 240, "-X25");
        K("klemens-tekli", 85, 240, "-X26");
        FieldInputs(
        [
            ("-PLC10", "I6.2", "MAX GAZ BASINÇ"), ("-PLC10", "I6.3", "MIN GAZ BASINÇ"),
            ("-PLC10", "I6.4", "MAX HAVA BASINÇ"), ("-PLC10", "I6.5", "MIN HAVA BASINÇ"),
            ("-PLC10", "I6.6", "İÇ BASINÇ"), ("-PLC10", "I6.7", "GAZ EMNİYET ŞALTER ARIZA"),
            ("-PLC10", "I7.0", "GAZ TAHLİYE SWİTCH KAPALI"), ("-PLC10", "I7.1", "GAZ TAHLİYE SWİTCH AÇIK"),
            ("-PLC10", "I7.2", "KAPI 1 ENDÜKTİF"), ("-PLC10", "I7.3", "KAPI 2 ENDÜKTİF"),
        ], "-X27");
    }

    /// <summary>Sayfa 16: valf röleleri.</summary>
    private void Valves()
    {
        NewPage("Valfler");
        In("DC0.9", 30);
        (string Ref, string Q, string Text)[] relays =
        [
            ("-K18", "Q5.4", "EMNİYET ŞALTERİ"), ("-K19", "Q5.5", "ANA GAZ VALFİ"),
            ("-K20", "Q5.6", "2. ANA GAZ VALFİ"), ("-K21", "Q5.7", "TEMİZ HAVA VALFİ"),
        ];
        for (var i = 0; i < relays.Length; i++)
        {
            var x = 65 + 100 * i;
            J(x, 30, i == relays.Length - 1 ? "LD" : "LRD");
            K("bobin", x, 70, relays[i].Ref, rotation: 180);
            Io(false, 'T', x, 140, "-PLC13", relays[i].Q, relays[i].Text);
        }
    }

    /// <summary>Sayfa 17: saha valfleri (230 VAC, röle kontaklarıyla).</summary>
    private void FieldValves()
    {
        NewPage("Saha Valfler");
        In("L1.3", 30, 50);
        In("N", 40, 50);
        string[] relays = ["-K18", "-K19", "-K20", "-K21"];
        for (var i = 0; i < relays.Length; i++)
        {
            var x = 55 + 45 * i;
            J(x, 30, i == relays.Length - 1 ? "LD" : "LRD");
            K("kontak-no", x, 140, relays[i]);
            if (i > 0)
            {
                var row = 155 + 10 * i;
                var terminal = 55 + 20 * i;
                J(x, row, "UL");
                J(terminal, row, "RD");
            }
        }
        K("klemens-valf-4", 55, 240, "-X28");
        J(340, 40, "LD");
        K("klemens-3lu-2", 330, 240, "-X29");
    }

    /// <summary>Cihaz kayıtları: açıklama, üretici, model ve kontak kapasitesi (özgün projedeki bilgiler).</summary>
    private void Records()
    {
        void Set(string reference, string description, string manufacturer = "", string model = "", string order = "", int contacts = 0)
        {
            var r = DeviceRegistry.Find(Doc, reference) ?? throw new InvalidOperationException(reference);
            r.Description = description;
            r.Manufacturer = manufacturer;
            r.Model = model;
            r.OrderNumber = order;
            r.Contacts = Enumerable.Range(1, contacts)
                .Select(i => new ContactSlot { Kind = ContactKind.No, Pins = [$"{i}3", $"{i}4"] }).ToList();
        }

        Set("-U1", "Besleme girişi");
        Set("-Q1", "Ana sigorta 4 kutup");
        Set("-PE", "PE klemensi");
        Set("-IT1", "L1 akım trafosu");
        Set("-IT2", "L2 akım trafosu");
        Set("-IT3", "L3 akım trafosu");
        Set("-D1", "Güç analizörü", "ENTES");
        Set("-DK1", "Faz sırası rölesi");
        Set("-F1", "22 kW invertör sigortası");
        Set("-F2", "2 kW invertör sigortası");
        Set("-F3", "Grup 1 sigortası");
        Set("-F4", "Grup 2 sigortası");
        Set("-F5", "24 VDC güç kaynağı sigortası");
        Set("-F6", "Panel PC sigortası");
        Set("-F7", "Priz sigortası");
        Set("-F8", "Pano sigortası");
        Set("-F9", "Kumanda sigortası");
        Set("-V1", "24 VDC güç kaynağı");
        Set("-X1", "24 VDC dağıtım klemensleri");
        Set("-X2", "0 V dağıtım klemensleri");
        Set("-ETH", "Ethernet switch");
        Set("-PR1", "Pano prizi");
        Set("-H1", "Pano aydınlatma");
        Set("-MF1", "Pano fanı");
        Set("-B1", "Işıklı start butonu");
        Set("-B2", "Işıklı stop butonu");
        Set("-B3", "Acil stop butonu");
        Set("-H2", "Işıklı ikaz (kırmızı / sarı / yeşil)");
        Set("-VPC", "Panel PC güç kaynağı");
        Set("-PC1", "Panel PC");
        Set("-PLC1", "PLC CPU", "Siemens", "CPU 1214C DC/DC/DC", "6ES7 214-1AG40-0XB0");
        Set("-PLC2", "Haberleşme adaptörü", "Beijer Electronics", "GN-9287");
        (string Ref, string Model, string Text)[] modules =
        [
            ("-PLC3", "GT-3114", "Analog giriş 4 kanal"), ("-PLC4", "GT-3114", "Analog giriş 4 kanal"),
            ("-PLC5", "GT-4114", "Analog çıkış 4 kanal"), ("-PLC6", "GT-4114", "Analog çıkış 4 kanal"),
            ("-PLC7", "GT-3804", "Analog giriş 4 kanal"),
            ("-PLC8", "GT-12DF", "Dijital giriş 16 kanal"), ("-PLC9", "GT-12DF", "Dijital giriş 16 kanal"),
            ("-PLC10", "GT-12DF", "Dijital giriş 16 kanal"), ("-PLC11", "GT-12DF", "Dijital giriş 16 kanal"),
            ("-PLC12", "GT-228F", "Dijital çıkış 16 kanal"), ("-PLC13", "GT-228F", "Dijital çıkış 16 kanal"),
            ("-PLC14", "GT-228F", "Dijital çıkış 16 kanal"),
        ];
        foreach (var m in modules)
        {
            Set(m.Ref, m.Text, "Beijer Electronics", m.Model);
        }
        Set("-S1", "Frekans inverteri 2,2 kW (baca fanı)", "CUMARK", "ES355");
        Set("-S2", "Frekans inverteri 22 kW (yakma fanı)", "CUMARK", "ES580");
        Set("-X3", "Motor 2 klemensleri");
        Set("-X4", "Motor 1 klemensleri");
        Set("-M1", "Motor 2 (2,2 kW)");
        Set("-M2", "Motor 1 (22 kW)");
        for (var i = 2; i <= 13; i++)
        {
            Set($"-K{i}", $"Brülör {i - 1} ateşleme rölesi", contacts: 1);
        }
        Set("-K14", "Grup 1 servo güç rölesi", contacts: 2);
        Set("-K15", "Grup 2 servo güç rölesi", contacts: 2);
        Set("-K16", "Temiz hava servo güç rölesi", contacts: 1);
        Set("-K17", "Brülör reset rölesi", contacts: 2);
        Set("-K18", "Emniyet şalteri rölesi", contacts: 1);
        Set("-K19", "Ana gaz valfi rölesi", contacts: 1);
        Set("-K20", "2. ana gaz valfi rölesi", contacts: 1);
        Set("-K21", "Temiz hava valfi rölesi", contacts: 1);
        for (var i = 5; i <= 16; i++)
        {
            Set($"-X{i}", $"Brülör {i - 4} saha klemensleri");
        }
        for (var i = 17; i <= 21; i++)
        {
            Set($"-X{i}", "Servo saha klemensleri");
        }
        Set("-X22", "Analog saha 24 V");
        Set("-X23", "Analog saha 0 V");
        Set("-X24", "Analog saha klemensleri");
        Set("-X25", "Dijital saha 24 V");
        Set("-X26", "Dijital saha 0 V");
        Set("-X27", "Dijital saha klemensleri");
        Set("-X28", "Saha valf klemensleri");
        Set("-X29", "Saha valf nötr klemensleri");
    }
}
