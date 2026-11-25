using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Model;
using static MYG.Electrical.Core.Tests.TestDevices;

namespace MYG.Electrical.Core.Tests;

public class AutoWireEngineTests
{
    private readonly AutoWireEngine _engine = new();

    private static bool Connected(IReadOnlyList<WireConnection> wires, DrawingObject a, DrawingObject b) =>
        wires.Any(w => (w.From.OwnerId == a.Id && w.To.OwnerId == b.Id) || (w.From.OwnerId == b.Id && w.To.OwnerId == a.Id));

    // 1
    [Fact]
    public void Facing_pins_on_same_horizontal_line_are_connected()
    {
        var a = WithPin("A", 0, 50, PinDirection.Right);
        var b = WithPin("B", 40, 50, PinDirection.Left);

        var wires = _engine.Compute([a, b]);

        var w = Assert.Single(wires);
        Assert.Equal(new Geometry.Point2D(0, 50), w.Start);
        Assert.Equal(new Geometry.Point2D(40, 50), w.End);
    }

    // 2
    [Fact]
    public void Facing_pins_on_same_vertical_line_are_connected()
    {
        var a = WithPin("A", 30, 10, PinDirection.Down);
        var b = WithPin("B", 30, 80, PinDirection.Up);

        var wires = _engine.Compute([a, b]);

        Assert.Single(wires);
        Assert.True(Connected(wires, a, b));
    }

    // 3
    [Theory]
    [InlineData(PinDirection.Right, PinDirection.Right)]
    [InlineData(PinDirection.Left, PinDirection.Left)]
    [InlineData(PinDirection.Left, PinDirection.Right)] // sırtı dönük
    public void Aligned_horizontal_pins_not_facing_each_other_are_not_connected(PinDirection first, PinDirection second)
    {
        var a = WithPin("A", 0, 0, first);
        var b = WithPin("B", 20, 0, second);

        Assert.Empty(_engine.Compute([a, b]));
    }

    [Theory]
    [InlineData(PinDirection.Up, PinDirection.Up)]
    [InlineData(PinDirection.Down, PinDirection.Down)]
    [InlineData(PinDirection.Up, PinDirection.Down)] // üstteki yukarı, alttaki aşağı bakıyor
    public void Aligned_vertical_pins_not_facing_each_other_are_not_connected(PinDirection upper, PinDirection lower)
    {
        var a = WithPin("A", 0, 0, upper);
        var b = WithPin("B", 0, 30, lower);

        Assert.Empty(_engine.Compute([a, b]));
    }

    [Fact]
    public void Perpendicular_pins_are_not_connected()
    {
        var a = WithPin("A", 0, 0, PinDirection.Right);
        var b = WithPin("B", 20, 0, PinDirection.Up);

        Assert.Empty(_engine.Compute([a, b]));
    }

    [Fact]
    public void Facing_pins_on_different_lines_are_not_connected()
    {
        var a = WithPin("A", 0, 0, PinDirection.Right);
        var b = WithPin("B", 20, 5, PinDirection.Left);

        Assert.Empty(_engine.Compute([a, b]));
    }

    // 4
    [Fact]
    public void K1_right_pin_connects_to_K2_left_pin()
    {
        var k1 = Horizontal("K1", 0, 0);
        var k2 = Horizontal("K2", 30, 0);

        var wires = _engine.Compute([k1, k2]);

        var w = Assert.Single(wires);
        Assert.Equal(new PinRef(k1.Id, "R"), w.From);
        Assert.Equal(new PinRef(k2.Id, "L"), w.To);
    }

    // 5
    [Fact]
    public void When_K2_has_no_right_pin_K3_left_pin_stays_free()
    {
        var k1 = Horizontal("K1", 0, 0);
        var k2 = Horizontal("K2", 30, 0, right: false);
        var k3 = Horizontal("K3", 60, 0);

        var wires = _engine.Compute([k1, k2, k3]);

        var w = Assert.Single(wires);
        Assert.True(Connected(wires, k1, k2));
        Assert.False(Connected(wires, k1, k3));
        Assert.DoesNotContain(wires, x => x.Involves(k3.Id));
    }

    // 6
    [Fact]
    public void Closer_suitable_pin_prevents_connection_to_farther_pin()
    {
        var source = WithPin("S", 0, 0, PinDirection.Right);
        var near = WithPin("N", 20, 0, PinDirection.Left);
        var far = WithPin("F", 50, 0, PinDirection.Left);

        var wires = _engine.Compute([far, source, near]);

        Assert.Single(wires);
        Assert.True(Connected(wires, source, near));
        Assert.False(Connected(wires, source, far));
    }

    [Fact]
    public void Chain_of_devices_connects_only_neighbours()
    {
        var k1 = Horizontal("K1", 0, 0);
        var k2 = Horizontal("K2", 30, 0);
        var k3 = Horizontal("K3", 60, 0);

        var wires = _engine.Compute([k3, k1, k2]);

        Assert.Equal(2, wires.Count);
        Assert.True(Connected(wires, k1, k2));
        Assert.True(Connected(wires, k2, k3));
        Assert.False(Connected(wires, k1, k3));
    }

    [Fact]
    public void Same_direction_pin_in_between_blocks_mutual_match()
    {
        // K1.R ... K2.R ... K3.L  → K3.L için en yakın uygun pin K2.R; K1.R boşta kalır.
        var k1 = WithPin("K1", 0, 0, PinDirection.Right);
        var k2 = WithPin("K2", 20, 0, PinDirection.Right);
        var k3 = WithPin("K3", 40, 0, PinDirection.Left);

        var wires = _engine.Compute([k1, k2, k3]);

        Assert.Single(wires);
        Assert.True(Connected(wires, k2, k3));
    }

    // 7
    [Fact]
    public void Rotating_device_rotates_pin_directions()
    {
        var k = Vertical("K", 0, 0);
        Assert.Equal([PinDirection.Up, PinDirection.Down], k.GetGlobalPins().Select(p => p.Direction));

        k.Rotation = 90;
        var pins = k.GetGlobalPins().ToList();
        Assert.Equal([PinDirection.Right, PinDirection.Left], pins.Select(p => p.Direction));
        Assert.Equal(10, pins[0].Position.X, 6); // üst pin (0,-10) → (10,0)
        Assert.Equal(0, pins[0].Position.Y, 6);
    }

    [Fact]
    public void Rotation_changes_which_pins_connect()
    {
        var top = Vertical("K1", 0, 0);
        var bottom = Vertical("K2", 0, 40);
        Assert.Single(_engine.Compute([top, bottom]));

        bottom.Rotation = 90; // pinleri artık yatay
        Assert.Empty(_engine.Compute([top, bottom]));
    }

    // 8
    [Fact]
    public void Moving_device_updates_wire_geometry()
    {
        var k1 = Vertical("K1", 50, 20);
        var k2 = Vertical("K2", 50, 80);
        var before = Assert.Single(_engine.Compute([k1, k2]));
        Assert.Equal(new Geometry.Point2D(50, 70), before.End);

        k2.Move(0, 20);
        var after = Assert.Single(_engine.Compute([k1, k2]));
        Assert.Equal(new Geometry.Point2D(50, 90), after.End);

        k2.Move(15, 0); // hizadan çıktı
        Assert.Empty(_engine.Compute([k1, k2]));
    }

    // 9
    [Fact]
    public void Removing_connection_point_removes_wire()
    {
        var k1 = Horizontal("K1", 0, 0);
        var k2 = Horizontal("K2", 30, 0);
        Assert.Single(_engine.Compute([k1, k2]));

        k2.ConnectionPoints.RemoveAll(c => c.Id == "L");
        Assert.Empty(_engine.Compute([k1, k2]));
    }

    [Fact]
    public void Removing_junction_removes_its_wires()
    {
        var a = WithPin("A", 0, 0, PinDirection.Right);
        var j = new JunctionObject { X = 20, Y = 0 };
        var below = WithPin("B", 20, 30, PinDirection.Up);
        var objects = new List<DrawingObject> { a, j, below };
        Assert.Equal(2, _engine.Compute(objects).Count);

        objects.Remove(j);
        Assert.Empty(_engine.Compute(objects));
    }

    // 10
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.3, true)]
    [InlineData(0.5, true)]
    [InlineData(0.51, false)]
    [InlineData(2.0, false)]
    public void Small_coordinate_differences_within_tolerance_are_accepted(double offset, bool expected)
    {
        var a = WithPin("A", 0, 0, PinDirection.Right);
        var b = WithPin("B", 25, offset, PinDirection.Left);

        Assert.Equal(expected, _engine.Compute([a, b]).Count == 1);
    }

    [Fact]
    public void Floating_point_noise_is_tolerated()
    {
        var a = WithPin("A", 0.1 + 0.2, 10, PinDirection.Down); // 0.30000000000000004
        var b = WithPin("B", 0.3, 40, PinDirection.Up);

        Assert.Single(_engine.Compute([a, b]));
    }

    [Fact]
    public void Junction_creates_T_connection()
    {
        // L1 ──●── L2
        //      │
        //      M
        var left = WithPin("L1", 0, 0, PinDirection.Right);
        var right = WithPin("L2", 40, 0, PinDirection.Left);
        var j = new JunctionObject { X = 20, Y = 0 };
        var down = WithPin("M", 20, 30, PinDirection.Up);

        var wires = _engine.Compute([left, right, j, down]);

        Assert.Equal(3, wires.Count);
        Assert.True(Connected(wires, left, j));
        Assert.True(Connected(wires, j, right));
        Assert.True(Connected(wires, j, down));
        Assert.False(Connected(wires, left, right));
    }

    [Fact]
    public void Junction_supports_four_way_connection()
    {
        var j = new JunctionObject { X = 50, Y = 50 };
        DrawingObject[] objects =
        [
            j,
            WithPin("W", 10, 50, PinDirection.Right),
            WithPin("E", 90, 50, PinDirection.Left),
            WithPin("N", 50, 10, PinDirection.Down),
            WithPin("S", 50, 90, PinDirection.Up),
        ];

        var wires = _engine.Compute(objects);

        Assert.Equal(4, wires.Count);
        Assert.All(wires, w => Assert.True(w.Involves(j.Id)));
    }

    [Fact]
    public void Junctions_chain_along_a_bus()
    {
        var src = WithPin("S", 0, 0, PinDirection.Right);
        var j1 = new JunctionObject { X = 20, Y = 0 };
        var j2 = new JunctionObject { X = 40, Y = 0 };
        var end = WithPin("E", 60, 0, PinDirection.Left);

        var wires = _engine.Compute([src, j1, j2, end]);

        Assert.Equal(3, wires.Count);
        Assert.True(Connected(wires, j1, j2));
    }

    [Fact]
    public void Pins_of_same_device_never_connect_to_each_other()
    {
        var d = new DeviceObject();
        d.ConnectionPoints.Add(new ConnectionPoint("A", 0, 0, PinDirection.Right));
        d.ConnectionPoints.Add(new ConnectionPoint("B", 10, 0, PinDirection.Left));

        Assert.Empty(_engine.Compute([d]));
    }

    [Fact]
    public void Contactor_column_over_motor_connects_three_phases()
    {
        var lib = MYG.Electrical.Symbols.BuiltInSymbolLibrary.Instance;
        var q = lib.Find(MYG.Electrical.Symbols.SymbolIds.CircuitBreaker)!.CreateDevice(100, 20, "-Q1");
        var k = lib.Find(MYG.Electrical.Symbols.SymbolIds.Contactor)!.CreateDevice(100, 60, "-K1");
        var m = lib.Find(MYG.Electrical.Symbols.SymbolIds.Motor)!.CreateDevice(100, 110, "-M1");

        var wires = _engine.Compute([q, k, m]);

        Assert.Equal(6, wires.Count);
        Assert.Equal(3, wires.Count(w => w.Involves(q.Id) && w.Involves(k.Id)));
        Assert.Equal(3, wires.Count(w => w.Involves(k.Id) && w.Involves(m.Id)));
        Assert.All(wires, w => Assert.Equal(w.Start.X, w.End.X, 9)); // hepsi dikey düz çizgi
    }

    [Fact]
    public void Performance_hundreds_of_devices_compute_quickly()
    {
        var objects = new List<DrawingObject>();
        for (var col = 0; col < 40; col++)
        {
            for (var row = 0; row < 10; row++)
            {
                objects.Add(Vertical($"K{col}_{row}", col * 10, row * 30));
            }
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var wires = _engine.Compute(objects);
        sw.Stop();

        Assert.Equal(40 * 9, wires.Count);
        Assert.True(sw.ElapsedMilliseconds < 200, $"{sw.ElapsedMilliseconds} ms");
    }
}
