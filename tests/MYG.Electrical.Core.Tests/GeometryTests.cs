using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Tests;

public class GeometryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(360, 0)]
    [InlineData(450, 90)]
    [InlineData(-90, 270)]
    [InlineData(89, 90)]
    [InlineData(-450, 270)]
    public void Rotation_is_normalized_to_quarter_turns(double input, int expected) =>
        Assert.Equal(expected, CoordinateTransform.NormalizeRotation(input));

    [Theory]
    [InlineData(PinDirection.Up, 90, PinDirection.Right)]
    [InlineData(PinDirection.Right, 90, PinDirection.Down)]
    [InlineData(PinDirection.Down, 90, PinDirection.Left)]
    [InlineData(PinDirection.Left, 90, PinDirection.Up)]
    [InlineData(PinDirection.Up, 180, PinDirection.Down)]
    [InlineData(PinDirection.Up, 270, PinDirection.Left)]
    [InlineData(PinDirection.Left, 0, PinDirection.Left)]
    public void Direction_rotates_clockwise(PinDirection input, double angle, PinDirection expected) =>
        Assert.Equal(expected, CoordinateTransform.RotateDirection(input, angle));

    [Fact]
    public void Rotated_point_matches_rotated_direction()
    {
        // Yukarı bakan (0,-10) pin 90° dönünce (10,0) konumunda ve sağa bakar.
        var p = CoordinateTransform.LocalToGlobal(new Point2D(0, -10), new Point2D(100, 100), 90);
        Assert.Equal(new Point2D(110, 100), p);
    }

    [Fact]
    public void Scale_is_applied_before_translation()
    {
        var p = CoordinateTransform.LocalToGlobal(new Point2D(5, 0), new Point2D(10, 10), 0, 2);
        Assert.Equal(new Point2D(20, 10), p);
    }

    [Theory]
    [InlineData(7.4, 5, 5)]
    [InlineData(7.6, 5, 10)]
    [InlineData(-2.4, 5, 0)]
    [InlineData(3.3, 0, 3.3)]
    public void Snap_rounds_to_grid(double value, double grid, double expected) =>
        Assert.Equal(expected, CoordinateTransform.Snap(value, grid), 9);

    [Fact]
    public void Distance_to_segment()
    {
        Assert.Equal(5, CoordinateTransform.DistanceToSegment(new(5, 5), new(0, 0), new(10, 0)), 9);
        Assert.Equal(5, CoordinateTransform.DistanceToSegment(new Point2D(15, 0), new(0, 0), new(10, 0)), 9);
    }

    [Fact]
    public void View_transform_round_trips()
    {
        var v = new ViewTransform();
        v.Pan(30, 40);
        v.ZoomAt(new Point2D(200, 150), 2.5);
        var world = new Point2D(123.4, 56.7);

        var back = v.ScreenToWorld(v.WorldToScreen(world));

        Assert.Equal(world.X, back.X, 9);
        Assert.Equal(world.Y, back.Y, 9);
    }

    [Fact]
    public void Zoom_keeps_point_under_cursor_fixed()
    {
        var v = new ViewTransform();
        var cursor = new Point2D(320, 240);
        var before = v.ScreenToWorld(cursor);

        v.ZoomAt(cursor, ViewTransform.DefaultZoomStep);
        v.ZoomAt(cursor, ViewTransform.DefaultZoomStep);
        var after = v.ScreenToWorld(cursor);

        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
        Assert.True(v.Zoom > 1.4);
    }

    [Fact]
    public void Zoom_is_clamped()
    {
        var v = new ViewTransform();
        for (var i = 0; i < 100; i++)
        {
            v.ZoomAt(Point2D.Zero, 2);
        }
        Assert.Equal(ViewTransform.MaxZoom, v.Zoom);
    }

    [Fact]
    public void Fit_centers_page_in_viewport()
    {
        var v = new ViewTransform();
        var page = new Rect2D(0, 0, 420, 297);
        v.FitTo(page, 1200, 800, 0);

        var center = v.WorldToScreen(page.Center);
        Assert.Equal(600, center.X, 6);
        Assert.Equal(400, center.Y, 6);
    }

    [Fact]
    public void Transformed_bounds_swap_extent_on_quarter_turn()
    {
        var r = CoordinateTransform.TransformBounds(new Rect2D(-5, -10, 10, 20), new Point2D(0, 0), 90);
        Assert.Equal(20, r.Width, 9);
        Assert.Equal(10, r.Height, 9);
    }

    [Theory]
    [InlineData(100, 50, 7.4, 0, 5, 0)]     // grid üzerindeki nesne: 7,4 mm → 5 mm
    [InlineData(101.3, 50, 2, 0, 3.7, 0)]   // grid dışındaki nesne (101,3): 103,3 → 105'e oturur
    [InlineData(100, 50, 2.4, -2.6, 0, -5)]
    public void Drag_delta_snaps_grabbed_point_onto_grid(double ox, double oy, double dx, double dy, double ex, double ey)
    {
        var d = CoordinateTransform.SnapDelta(new Point2D(ox, oy), new Point2D(dx, dy), 5);
        Assert.Equal(ex, d.X, 9);
        Assert.Equal(ey, d.Y, 9);
    }

    [Fact]
    public void Snap_point_is_insertion_point_start_or_top_left()
    {
        Assert.Equal(new Point2D(1, 2), new LineObject { X1 = 1, Y1 = 2, X2 = 8, Y2 = 2 }.SnapPoint);
        Assert.Equal(new Point2D(3, 4), new RectangleObject { X = 3, Y = 4, Width = 7, Height = 9 }.SnapPoint);
        Assert.Equal(new Point2D(5, 6), new DeviceObject { X = 5, Y = 6 }.SnapPoint);
        Assert.Equal(new Point2D(7, 8), new ImageObject { X = 7, Y = 8, Width = 3, Height = 3 }.SnapPoint);
    }
}
