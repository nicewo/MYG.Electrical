namespace MYG.Electrical.Core.Geometry;

/// <summary>Projede kullanılan sabit geometrik değerler. Magic number kullanmamak için tek yerde toplanır.</summary>
public static class GeometryConstants
{
    /// <summary>Otomatik kablolamada iki koordinatın "aynı hat" sayılması için tolerans (mm).</summary>
    public const double ConnectionToleranceMm = 0.5;

    /// <summary>Varsayılan grid aralığı (mm).</summary>
    public const double DefaultGridSpacingMm = 5.0;

    /// <summary>Fare ile nesne seçerken kabul edilen ekran mesafesi (piksel).</summary>
    public const double HitTolerancePixels = 5.0;

    /// <summary>WPF cihazdan bağımsız piksel sayısı / mm (96 DPI).</summary>
    public const double DipPerMm = 96.0 / 25.4;

    /// <summary>PDF point sayısı / mm (72 DPI).</summary>
    public const double PointPerMm = 72.0 / 25.4;

    public const double RightAngle = 90.0;
    public const double FullTurn = 360.0;

    /// <summary>Metin genişliği tahmini için karakter başına yükseklik oranı.</summary>
    public const double AverageCharWidthRatio = 0.6;
}
