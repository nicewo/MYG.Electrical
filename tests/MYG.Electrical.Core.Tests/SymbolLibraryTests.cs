using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class SymbolLibraryTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;

    [Fact]
    public void Library_contains_all_required_symbols()
    {
        string[] required =
        [
            SymbolIds.Contactor, SymbolIds.Relay, SymbolIds.Fuse, SymbolIds.CircuitBreaker, SymbolIds.Motor,
            SymbolIds.PushButton, SymbolIds.EmergencyStop, SymbolIds.LimitSwitch, SymbolIds.Sensor,
            SymbolIds.Terminal, SymbolIds.Lamp, SymbolIds.Coil, SymbolIds.NormallyOpen, SymbolIds.NormallyClosed,
        ];
        Assert.All(required, id => Assert.NotNull(Lib.Find(id)));
        Assert.Equal(Lib.All.Count, Lib.All.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Every_symbol_has_unique_pins_on_grid()
    {
        foreach (var s in Lib.All)
        {
            Assert.NotEmpty(s.ConnectionPoints);
            Assert.Equal(s.ConnectionPoints.Count, s.ConnectionPoints.Select(c => c.Id).Distinct().Count());
            foreach (var c in s.ConnectionPoints)
            {
                Assert.Equal(c.X, CoordinateTransform.Snap(c.X, GeometryConstants.DefaultGridSpacingMm), 9);
                Assert.Equal(c.Y, CoordinateTransform.Snap(c.Y, GeometryConstants.DefaultGridSpacingMm), 9);
            }
        }
    }

    [Fact]
    public void Symbol_bounds_include_pins()
    {
        foreach (var s in Lib.All)
        {
            var b = s.LocalBounds.Inflate(1e-9);
            Assert.All(s.ConnectionPoints, c => Assert.True(b.Contains(new Point2D(c.X, c.Y)), $"{s.Id}:{c.Id}"));
        }
    }

    [Fact]
    public void Created_device_copies_pins_independently()
    {
        var def = Lib.Find(SymbolIds.Coil)!;
        var a = def.CreateDevice(0, 0, "-K1");
        a.ConnectionPoints[0].X = 99;
        Assert.NotEqual(99, def.ConnectionPoints[0].X);
    }
}
