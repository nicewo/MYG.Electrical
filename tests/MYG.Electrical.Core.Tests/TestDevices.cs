using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Tests;

/// <summary>Testler için basit cihaz fabrikası.</summary>
internal static class TestDevices
{
    /// <summary>Sol ve/veya sağ pini olan yatay cihaz (pinler ±halfWidth, y=0).</summary>
    public static DeviceObject Horizontal(string reference, double x, double y, bool left = true, bool right = true, double halfWidth = 5)
    {
        var d = new DeviceObject { Reference = reference, SymbolId = "test.h", X = x, Y = y };
        if (left)
        {
            d.ConnectionPoints.Add(new ConnectionPoint("L", -halfWidth, 0, PinDirection.Left));
        }
        if (right)
        {
            d.ConnectionPoints.Add(new ConnectionPoint("R", halfWidth, 0, PinDirection.Right));
        }
        return d;
    }

    /// <summary>Üst (Up) ve/veya alt (Down) pini olan dikey cihaz.</summary>
    public static DeviceObject Vertical(string reference, double x, double y, bool top = true, bool bottom = true, double halfHeight = 10)
    {
        var d = new DeviceObject { Reference = reference, SymbolId = "test.v", X = x, Y = y };
        if (top)
        {
            d.ConnectionPoints.Add(new ConnectionPoint("T", 0, -halfHeight, PinDirection.Up));
        }
        if (bottom)
        {
            d.ConnectionPoints.Add(new ConnectionPoint("B", 0, halfHeight, PinDirection.Down));
        }
        return d;
    }

    public static DeviceObject WithPin(string reference, double x, double y, PinDirection dir)
    {
        var d = new DeviceObject { Reference = reference, SymbolId = "test.p", X = x, Y = y };
        d.ConnectionPoints.Add(new ConnectionPoint("P", 0, 0, dir));
        return d;
    }
}
