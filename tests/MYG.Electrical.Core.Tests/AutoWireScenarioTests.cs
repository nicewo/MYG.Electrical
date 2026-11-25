using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

/// <summary>Gerçek makine şemalarından alınmış devre düzenleriyle otomatik kablolama.</summary>
public class AutoWireScenarioTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;
    private readonly AutoWireEngine _engine = new();

    private static DeviceObject Dev(string symbolId, double x, double y, string reference, double rotation = 0)
    {
        var d = Lib.Find(symbolId)!.CreateDevice(x, y, reference);
        d.Rotation = rotation;
        return d;
    }

    private static JunctionObject J(JunctionShape shape, double rotation, double x, double y) =>
        new() { Shape = shape, Rotation = rotation, X = x, Y = y };

    private static HashSet<(string, string, string, string)> Describe(IEnumerable<WireConnection> wires, IEnumerable<DrawingObject> objects)
    {
        var names = objects.ToDictionary(o => o.Id, o => o switch
        {
            DeviceObject d => d.Reference,
            JunctionObject j => $"J({j.X},{j.Y})",
            _ => o.GetType().Name,
        });
        return wires.Select(w => (names[w.From.OwnerId], w.From.PinId, names[w.To.OwnerId], w.To.PinId)).ToHashSet();
    }

    /// <summary>Direkt yol verme güç devresi: Q1 → K1 → X1..X3 → M1, her faz ayrı hat.</summary>
    [Fact]
    public void Direct_on_line_motor_starter_power_circuit()
    {
        DrawingObject[] objects =
        [
            Dev(SymbolIds.CircuitBreaker, 100, 40, "-Q1"),
            Dev(SymbolIds.Contactor, 100, 80, "-K1"),
            Dev(SymbolIds.Terminal, 90, 115, "-X1:1"),
            Dev(SymbolIds.Terminal, 100, 115, "-X1:2"),
            Dev(SymbolIds.Terminal, 110, 115, "-X1:3"),
            Dev(SymbolIds.Motor, 100, 145, "-M1"),
        ];

        var wires = _engine.Compute(objects);
        var d = Describe(wires, objects);

        Assert.Equal(9, wires.Count);
        Assert.Contains(("-Q1", "2", "-K1", "1"), d);
        Assert.Contains(("-Q1", "6", "-K1", "5"), d);
        Assert.Contains(("-K1", "4", "-X1:2", "1"), d);
        Assert.Contains(("-X1:1", "2", "-M1", "U1"), d);
        Assert.Contains(("-X1:3", "2", "-M1", "W1"), d);
        // Fazlar asla birbirine karışmaz: her kablo dikeydir
        Assert.All(wires, w => Assert.Equal(w.Start.X, w.End.X));
    }

    /// <summary>
    /// Mühürlemeli (self-hold) kumanda devresi:
    ///   F1 – S1(acil) – ├──────┐
    ///                   S2     K1 13-14 (mühürleme)
    ///                   ├──────┘
    ///                   K1 bobin
    /// </summary>
    private static DrawingObject[] SelfHoldCircuit(out JunctionObject upperTee, out JunctionObject lowerTee) =>
    [
        Dev(SymbolIds.Fuse, 100, 20, "-F1"),
        Dev(SymbolIds.EmergencyStop, 100, 50, "-S1"),
        upperTee = J(JunctionShape.Tee, 270, 100, 75),   // ├ yukarı + aşağı + sağ
        J(JunctionShape.Angle, 90, 130, 75),              // ┐ sol + aşağı
        Dev(SymbolIds.PushButton, 100, 95, "-S2"),
        Dev(SymbolIds.NormallyOpen, 130, 95, "-K1.1"),
        lowerTee = J(JunctionShape.Tee, 270, 100, 115),  // ├
        J(JunctionShape.Angle, 180, 130, 115),            // ┘ sol + yukarı
        Dev(SymbolIds.Coil, 100, 135, "-K1"),
    ];

    [Fact]
    public void Self_hold_control_circuit_with_tee_and_angles()
    {
        var objects = SelfHoldCircuit(out _, out _);

        var wires = _engine.Compute(objects);
        var d = Describe(wires, objects);

        Assert.Equal(9, wires.Count);
        Assert.Contains(("-F1", "2", "-S1", "11"), d);
        Assert.Contains(("-S1", "12", "J(100,75)", "Up"), d);
        Assert.Contains(("J(100,75)", "Right", "J(130,75)", "Left"), d);
        Assert.Contains(("J(130,75)", "Down", "-K1.1", "13"), d);
        Assert.Contains(("-K1.1", "14", "J(130,115)", "Up"), d);
        Assert.Contains(("J(100,115)", "Right", "J(130,115)", "Left"), d);
        Assert.Contains(("J(100,115)", "Down", "-K1", "A1"), d);
    }

    [Fact]
    public void Removing_a_tee_breaks_only_its_branch_and_undo_restores_it()
    {
        var doc = ProjectDocument.CreateNew();
        var s = new EditorSession(doc, Lib);
        var circuit = SelfHoldCircuit(out var upperTee, out _);
        foreach (var o in circuit)
        {
            s.AddObject(o, select: false);
        }
        var before = s.Wires.Count;

        s.Select(upperTee);
        s.DeleteSelection();
        // S1↔T, T↔S2, T↔dirsek kopar; S1 ile S2 aynı hatta ve birbirine baktığı için doğrudan bağlanır.
        Assert.Equal(before - 3 + 1, s.Wires.Count);
        Assert.DoesNotContain(s.Wires, w => w.Involves(upperTee.Id));
        var s1 = circuit.OfType<DeviceObject>().Single(d => d.Reference == "-S1");
        var s2 = circuit.OfType<DeviceObject>().Single(d => d.Reference == "-S2");
        Assert.Contains(s.Wires, w => w.Involves(s1.Id) && w.Involves(s2.Id));
        var corner = circuit.OfType<JunctionObject>().Single(j => j.X == 130 && j.Y == 75);
        Assert.DoesNotContain(s.Wires, w => w.To == new PinRef(corner.Id, "Left")); // dirseğin sol ucu boşta

        s.Undo();
        Assert.Equal(before, s.Wires.Count);
    }

    [Fact]
    public void Rotating_junction_full_turn_restores_same_connections()
    {
        var objects = SelfHoldCircuit(out var tee, out _);
        var original = Describe(_engine.Compute(objects), objects);

        for (var i = 0; i < 4; i++)
        {
            ((IRotatable)tee).Rotation += 90;
            Assert.True(_engine.Compute(objects).Count <= original.Count);
        }

        Assert.Equal(original, Describe(_engine.Compute(objects), objects));
    }

    [Fact]
    public void Result_does_not_depend_on_object_order()
    {
        var objects = SelfHoldCircuit(out _, out _).ToList();
        var expected = Describe(_engine.Compute(objects), objects);
        var rnd = new Random(3);

        for (var i = 0; i < 25; i++)
        {
            var shuffled = objects.OrderBy(_ => rnd.Next()).ToList();
            Assert.Equal(expected, Describe(_engine.Compute(shuffled), shuffled));
        }
    }

    [Fact]
    public void Wire_ends_sit_exactly_on_pin_positions()
    {
        var objects = SelfHoldCircuit(out _, out _);
        var pins = AutoWireEngine.CollectPins(objects).ToDictionary(p => p.Ref, p => p.Position);

        foreach (var w in _engine.Compute(objects))
        {
            Assert.Equal(pins[w.From], w.Start);
            Assert.Equal(pins[w.To], w.End);
        }
    }

    [Fact]
    public void Equidistant_candidates_are_resolved_deterministically()
    {
        // İki hedef pin aynı noktada: her çalıştırmada aynısı seçilmeli ve yalnızca bir kablo çizilmeli
        var src = Dev(SymbolIds.Coil, 50, 0, "-A");
        var t1 = Dev(SymbolIds.Coil, 50, 40, "-B");
        var t2 = Dev(SymbolIds.Coil, 50, 40, "-C");
        DrawingObject[] objects = [src, t1, t2];

        var first = Describe(_engine.Compute(objects), objects);
        Assert.Single(first, x => x.Item1 == "-A");
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(first, Describe(_engine.Compute(objects.Reverse().ToArray()), objects));
        }
    }

    [Fact]
    public void Device_rotated_horizontal_connects_in_a_row()
    {
        // Yatay çizilmiş sigorta dizisi (90° döndürülmüş): F1 → F2 → F3
        DrawingObject[] objects =
        [
            Dev(SymbolIds.Fuse, 40, 50, "-F1", 90),
            Dev(SymbolIds.Fuse, 70, 50, "-F2", 90),
            Dev(SymbolIds.Fuse, 100, 50, "-F3", 90),
        ];

        var wires = _engine.Compute(objects);

        Assert.Equal(2, wires.Count);
        Assert.All(wires, w => Assert.Equal(w.Start.Y, w.End.Y));
    }

    [Fact]
    public void Placement_tolerance_matches_engine_tolerance()
    {
        Assert.Equal(GeometryConstants.ConnectionToleranceMm, new AutoWireEngine().Tolerance);
        // 0,4 mm kaymış pin: hem motor bağlar hem de yön önerisi onu görür
        var a = Dev(SymbolIds.Coil, 50.4, 0, "-A"); // A2 (50.4, 10) aşağı bakar
        var rot = JunctionPlacement.SuggestRotation([a], new Point2D(50, 40), JunctionShape.Angle, 0);
        Assert.Contains(PinDirection.Up, new JunctionObject { Shape = JunctionShape.Angle, Rotation = rot }.ActiveDirections);
    }

    [Fact]
    public void Long_busbar_with_many_tees_stays_fast()
    {
        // En kötü durum: tek hatta 600 T bağlantısı (aynı satırda 1200 yatay pin), her birinin altında bir bobin
        var objects = new List<DrawingObject>();
        for (var i = 0; i < 600; i++)
        {
            objects.Add(J(JunctionShape.Tee, 0, i * 10, 0));
            objects.Add(Dev(SymbolIds.Coil, i * 10, 30, $"-K{i}"));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var wires = _engine.Compute(objects);
        sw.Stop();

        Assert.Equal(599 + 600, wires.Count);
        Assert.True(sw.ElapsedMilliseconds < 500, $"{sw.ElapsedMilliseconds} ms");
    }
}
