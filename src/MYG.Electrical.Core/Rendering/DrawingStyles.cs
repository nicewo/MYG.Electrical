namespace MYG.Electrical.Core.Rendering;

/// <summary>Teknik çizim renk ve kalınlıkları (kağıt üzerindeki gerçek mm).</summary>
public static class DrawingStyles
{
    public const uint Black = 0xFF000000;
    public const uint DarkGray = 0xFF404040;
    public const uint MidGray = 0xFF808080;
    public const uint White = 0xFFFFFFFF;
    public const uint PinMarker = 0xFFD9480F;
    public const uint WireColor = 0xFF1C4E9C;
    public const uint ReferenceColor = 0xFF1A1A1A;

    public const double SymbolLine = 0.35;
    public const double WireLine = 0.35;
    public const double FrameLine = 0.5;
    public const double ThinLine = 0.25;

    public const double ReferenceTextHeight = 3.0;
    public const double PinTextHeight = 2.0;
    public const double PinMarkerSize = 0.6;

    /// <summary>Editörde junction yönlerini gösteren kısa çizgilerin uzunluğu (mm).</summary>
    public const double JunctionStubLength = 2.5;

    public static readonly StrokeStyle Symbol = new(Black, SymbolLine);
    public static readonly StrokeStyle Wire = new(WireColor, WireLine);
    public static readonly StrokeStyle Frame = new(Black, FrameLine);
    public static readonly StrokeStyle Thin = new(Black, ThinLine);
}
