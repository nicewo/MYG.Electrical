using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>
/// Kabul testleri için örnek makine projesi: 22 kW ve 2.2 kW sürücüler, enerji analizörü, akım trafoları,
/// PLC CPU + DI/DO, Delta HMI, 24 VDC dağıtım, start/stop/acil stop, kontaktör + yardımcı kontak, termik,
/// faz sırası rölesi, basınç şalteri, endüktif sensör, kapı sensörü, saha klemensleri. Bütün bağlantılar
/// otomatik kablolamayla (hizalı pinler) kurulur; potansiyeller ve sayfa bağlantılarıyla sayfalar birleşir.
/// </summary>
public sealed class ReferenceMachine
{
    public static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;
    public static readonly DeviceLibrary Devices = BuiltInDeviceLibrary.Load(Lib);

    public EditorSession Session { get; }
    public ProjectDocument Doc => Session.Document;
    public Page Power { get; }
    public Page Power2 { get; }
    public Page Supply { get; }
    public Page Control { get; }
    public Page Inputs { get; }
    public Page Outputs { get; }

    public DeviceObject Vfd22 { get; private set; } = null!;
    public DeviceObject Vfd2 { get; private set; } = null!;
    public DeviceObject Coil { get; private set; } = null!;
    public DeviceObject MainContacts { get; private set; } = null!;
    public DeviceObject AuxContact { get; private set; } = null!;
    public DeviceObject Overload { get; private set; } = null!;
    public DeviceObject OverloadContact { get; private set; } = null!;
    public DeviceObject Cpu { get; private set; } = null!;
    public DeviceObject DiModule { get; private set; } = null!;
    public DeviceObject DoModule { get; private set; } = null!;
    public DeviceObject PhaseRelay { get; private set; } = null!;
    public DeviceObject Analyzer { get; private set; } = null!;

    public ReferenceMachine()
    {
        var doc = ProjectDocument.CreateNew("Hat 1 Pompa İstasyonu");
        doc.Project.MachineName = "PST-01";
        doc.Pages[1].Name = "Güç 1 — sürücüler";
        Power = doc.Pages[1];
        Power2 = Add(doc, "Güç 2 — DOL motor");
        Supply = Add(doc, "24 VDC besleme");
        Control = Add(doc, "Kumanda");
        Inputs = Add(doc, "PLC girişleri");
        Outputs = Add(doc, "PLC çıkışları ve HMI");
        Session = new EditorSession(doc, Lib);

        BuildPower();
        BuildPower2();
        BuildSupply();
        BuildControl();
        BuildInputs();
        BuildOutputs();
    }

    private static Page Add(ProjectDocument doc, string name)
    {
        var p = Page.Create(name, PageType.Schematic);
        doc.Pages.Add(p);
        return p;
    }

    private DeviceObject Dev(string templateId, double x, double y, double rot = 0) =>
        Session.PlaceDevice(Devices.Find(templateId) ?? throw new InvalidOperationException(templateId), new Point2D(x, y), rot)!;

    private DeviceObject Sym(string symbolId, double x, double y, double rot = 0) =>
        Session.PlaceSymbol(Lib.Find(symbolId) ?? throw new InvalidOperationException(symbolId), new Point2D(x, y), rot);

    private void Pot(string name, PotentialKind kind, double x, double y, double rot = 0, string voltage = "") =>
        Session.AddObject(new PotentialObject { X = x, Y = y, Rotation = rot, Name = name, PotentialKind = kind, Voltage = voltage });

    private void Link(string name, double x, double y, double rot) =>
        Session.AddObject(new PageLinkObject { X = x, Y = y, Rotation = rot, Name = name });

    private void Phases(double x0, double y, double step = 10, bool pe = false, bool n = false)
    {
        Pot("L1", PotentialKind.L1, x0, y, voltage: "400VAC");
        Pot("L2", PotentialKind.L2, x0 + step, y, voltage: "400VAC");
        Pot("L3", PotentialKind.L3, x0 + 2 * step, y, voltage: "400VAC");
        if (n)
        {
            Pot("N", PotentialKind.N, x0 + 3 * step, y, voltage: "230VAC");
        }
        if (pe)
        {
            Pot("PE", PotentialKind.PE, x0 + 3 * step, y);
        }
    }

    /// <summary>Sayfa 2: 22 kW sürücü + motor, 2.2 kW sürücü + motor, faz sırası rölesi, akım trafoları, enerji analizörü.</summary>
    private void BuildPower()
    {
        Session.ActivePage = Power;
        // 22 kW: L1 L2 L3 PE → C63 3P → VFD → motor
        Phases(60, 30, pe: true);
        Dev("dev.mcb.c63.3p", 70, 60);
        Vfd22 = Dev("dev.vfd.22kw", 75, 100);         // üst pinler y=80: 60, 70, 80, 90(PE1)
        Dev("dev.motor.22kw", 70, 140);              // U1 V1 W1 PE (130)

        // 2.2 kW (monofaze giriş): L N PE → C16 2P → VFD 1ph → motor
        Pot("L1", PotentialKind.L1, 140, 30, voltage: "230VAC");
        Pot("N", PotentialKind.N, 150, 30, voltage: "230VAC");
        Pot("PE", PotentialKind.PE, 160, 30);
        Dev("dev.rcd.2p.25a.30ma", 145, 60);          // kaçak akım 2P: 140 / 150
        Vfd2 = Dev("dev.vfd.2.2kw.1ph", 150, 100);    // L(140) N(150) PE(160) y=80; çıkış 135..165 y=120
        Dev("dev.motor.2.2kw", 145, 140);            // U1 135, V1 145, W1 155, PE 165

        // Faz sırası rölesi: kontak 14 → PLC girişine sayfa bağlantısı
        Phases(190, 60);
        PhaseRelay = Dev("dev.relay.phase", 200, 90);  // L1 190, L2 200, L3 210 (y=75); 11 12 14 (y=105)
        Link("FAZ-OK", 210, 125, 90);

        // Akım trafoları (S1/S2 analizöre ayrı kablolanır, burada saha klemensine çıkar)
        foreach (var (x, name, kind) in new[] { (330.0, "L1", PotentialKind.L1), (360.0, "L2", PotentialKind.L2), (390.0, "L3", PotentialKind.L3) })
        {
            Pot(name, kind, x, 30, voltage: "400VAC");
            Dev("dev.ct.100", x, 55);                 // P1 45, P2 65
            Dev("dev.terminal.2.5", x, 75);           // 70 / 80
        }

        // Enerji analizörü: gerilim girişleri V1 V2 V3 VN (y=180)
        Phases(260, 155);
        Pot("N", PotentialKind.N, 290, 155, voltage: "230VAC");
        Analyzer = Dev("dev.analyzer.modbus", 285, 200); // V1 260, V2 270, V3 280, VN 290, A1 300, A2 310
    }

    /// <summary>Sayfa 3: kontaktörle yol verilen motor (motor koruma şalteri, ana kontaklar, termik).</summary>
    private void BuildPower2()
    {
        Session.ActivePage = Power2;
        Phases(60, 30, pe: true);
        Dev("dev.mpcb.4-6.3", 70, 60);
        MainContacts = Sym("iec.contactor.3p", 70, 100);
        Overload = Dev("dev.overload.9-13", 70, 140);
        Dev("dev.motor.2.2kw", 70, 180);
    }

    /// <summary>Sayfa 4: 24 VDC güç kaynağı ve sigortalı dağıtım.</summary>
    private void BuildSupply()
    {
        Session.ActivePage = Supply;
        Pot("L1", PotentialKind.L1, 160, 115, voltage: "230VAC");
        Pot("N", PotentialKind.N, 170, 115, voltage: "230VAC");
        Pot("PE", PotentialKind.PE, 180, 115);
        Dev("dev.psu.24v.10a", 170, 150);              // L 160, N 170, PE 180 (y=135); +1 155, +2 165, -1 175, -2 185 (y=165)
        Dev("dev.dist.24v", 155, 190);                 // +IN (155,180); çıkış 140..170 (y=200)
        Link("+24V", 140, 215, 90);
        Pot("+24V", PotentialKind.DcPositive, 170, 215, 180, "24VDC");
        Pot("0V", PotentialKind.DcNegative, 175, 180, 180, "24VDC");
        Link("0V", 185, 180, 90);
    }

    /// <summary>Sayfa 5: start/stop/acil stop, termik kontağı, kontaktör bobini; yardımcı kontakla çalışma lambası.</summary>
    private void BuildControl()
    {
        Session.ActivePage = Control;
        Link("+24V", 60, 30, 270);
        Dev("dev.mcb.c2.1p", 60, 50);
        Dev("dev.button.estop", 60, 80);
        Dev("dev.button.stop", 60, 110);
        OverloadContact = Sym("iec.overload.contact.nc", 60, 140);
        Dev("dev.button.start", 60, 170);
        Coil = Dev("dev.contactor.9a.24vdc", 60, 200);
        Link("0V", 60, 225, 90);

        Pot("+24V", PotentialKind.DcPositive, 140, 30, voltage: "24VDC");
        AuxContact = Sym("iec.contact.no", 140, 60);
        Dev("dev.lamp.green.24v", 140, 90);
        Pot("0V", PotentialKind.DcNegative, 140, 120, 180);

        // Çok parçalı cihazlar: ana kontaklar ve yardımcı kontak bobinin, termik kontağı termiğin parçası
        Session.RenameReference(AuxContact, Coil.Reference);
        Session.ActivePage = Power2;
        Session.RenameReference(MainContacts, Coil.Reference);
        Session.ActivePage = Control;
        Session.RenameReference(OverloadContact, Overload.Reference);
    }

    /// <summary>Sayfa 6: PLC CPU, DI modülü, kapı ve basınç şalteri, endüktif sensör, faz rölesi girişi.</summary>
    private void BuildInputs()
    {
        Session.ActivePage = Inputs;
        Cpu = Dev("dev.plc.cpu", 340, 100);           // L+ 330, M 340, PE 350 (y=75)
        Pot("+24V", PotentialKind.DcPositive, 330, 50, voltage: "24VDC");
        Pot("0V", PotentialKind.DcNegative, 340, 50);
        Pot("PE", PotentialKind.PE, 350, 50);

        DiModule = Dev("dev.plc.di8", 200, 150);      // DI0..DI7: 165..235 (y=135); 1M (200,165)
        Pot("+24V", PotentialKind.DcPositive, 165, 40, voltage: "24VDC");
        Dev("dev.door.reed", 165, 90);                // 13 (80) / 14 (100)
        Pot("+24V", PotentialKind.DcPositive, 185, 40, voltage: "24VDC");
        Dev("dev.pressure.switch", 185, 90);
        // Endüktif sensör: BN +24V, BU 0V, BK → dirsek → DI4 (205)
        Pot("+24V", PotentialKind.DcPositive, 120, 70, voltage: "24VDC");
        Dev("dev.prox.m18.pnp", 120, 100);            // BN (120,90), BU (120,110), BK (130,100)
        Pot("0V", PotentialKind.DcNegative, 120, 130, 180);
        Session.PlaceJunction(JunctionShape.Angle, new Point2D(205, 100), 90); // sol + aşağı
        Pot("0V", PotentialKind.DcNegative, 200, 190, 180);

        // Faz sırası rölesi kontağı: sayfa bağlantısı → tek kanal PLC girişi
        Link("FAZ-OK", 300, 60, 270);
        var input = Sym("iec.plc.input", 300, 80);
        Session.RenameReference(input, Cpu.Reference);
        Session.UpdateDevice(input, DeviceRegistry.Find(Doc, Cpu.Reference)!.Clone(), "Faz sırası doğru",
            new Dictionary<string, string> { ["1"] = "I1.0" });
    }

    /// <summary>Sayfa 7: DO modülü, ara röle ve selenoid valf, Delta HMI.</summary>
    private void BuildOutputs()
    {
        Session.ActivePage = Outputs;
        DoModule = Dev("dev.plc.do8", 200, 100);      // 1L+ (195,85), 1M (205,85); DO0..7 165..235 (y=115)
        Pot("+24V", PotentialKind.DcPositive, 195, 60, voltage: "24VDC");
        Pot("0V", PotentialKind.DcNegative, 205, 60);
        Dev("dev.relay.2co.24vdc", 165, 140);         // A1 130 / A2 150
        Pot("0V", PotentialKind.DcNegative, 165, 170, 180);
        Sym("iec.valve.solenoid", 175, 140);
        Pot("0V", PotentialKind.DcNegative, 175, 170, 180);

        Dev("dev.hmi.delta.7", 320, 100);             // +24V 310, 0V 320, PE 330 (y=80)
        Pot("+24V", PotentialKind.DcPositive, 310, 55, voltage: "24VDC");
        Pot("0V", PotentialKind.DcNegative, 320, 55);
        Pot("PE", PotentialKind.PE, 330, 55);
    }
}
