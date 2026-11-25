namespace MYG.Electrical.Core.Geometry;

/// <summary>
/// Dünya (mm) ↔ ekran (DIP) dönüşümü. Ekran koordinatları yalnızca render aşamasında bu sınıf ile hesaplanır.
/// Screen = World * Scale + Offset; Scale = DipPerMm * Zoom.
/// </summary>
public sealed class ViewTransform
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 40.0;
    public const double DefaultZoomStep = 1.2;

    public double Zoom { get; private set; } = 1.0;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }

    /// <summary>1 mm'nin ekrandaki DIP karşılığı.</summary>
    public double Scale => GeometryConstants.DipPerMm * Zoom;

    public event EventHandler? Changed;

    public Point2D WorldToScreen(Point2D world) => new(world.X * Scale + OffsetX, world.Y * Scale + OffsetY);

    public Point2D ScreenToWorld(Point2D screen) => new((screen.X - OffsetX) / Scale, (screen.Y - OffsetY) / Scale);

    public double ScreenLengthToWorld(double pixels) => pixels / Scale;

    /// <summary>Ekrandaki sabit bir noktayı (fare imleci) sabit tutarak yakınlaştırır.</summary>
    public void ZoomAt(Point2D screenAnchor, double factor)
    {
        var worldAnchor = ScreenToWorld(screenAnchor);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        OffsetX = screenAnchor.X - worldAnchor.X * Scale;
        OffsetY = screenAnchor.Y - worldAnchor.Y * Scale;
        OnChanged();
    }

    public void Pan(double dxPixels, double dyPixels)
    {
        OffsetX += dxPixels;
        OffsetY += dyPixels;
        OnChanged();
    }

    /// <summary>Dünya dikdörtgenini verilen ekran boyutuna kenar boşluğu ile sığdırır.</summary>
    public void FitTo(Rect2D world, double viewportWidth, double viewportHeight, double marginPixels = 20)
    {
        if (world.Width <= 0 || world.Height <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }
        var availableW = Math.Max(1, viewportWidth - 2 * marginPixels);
        var availableH = Math.Max(1, viewportHeight - 2 * marginPixels);
        var scale = Math.Min(availableW / world.Width, availableH / world.Height);
        Zoom = Math.Clamp(scale / GeometryConstants.DipPerMm, MinZoom, MaxZoom);
        OffsetX = (viewportWidth - world.Width * Scale) / 2 - world.X * Scale;
        OffsetY = (viewportHeight - world.Height * Scale) / 2 - world.Y * Scale;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
