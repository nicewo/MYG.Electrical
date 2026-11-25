using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>
/// Sayfaya yerleştirilmiş resim. Veri projeye gömülü kaynaktan okunur (<see cref="Resources.ProjectResource"/>).
/// Saydamlık tahribatsızdır: orijinal (<see cref="SourceResourceId"/>) saklanır, işlenmiş hali
/// (<see cref="RenderResourceId"/>) ayrıca tutulur ve ayar değişince yeniden üretilir.
/// </summary>
public sealed class ImageObject : DrawingObject
{
    public const double DefaultTolerance = 10;

    /// <summary>Sol üst köşe (mm).</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Kullanıcının eklediği orijinal resim.</summary>
    public string SourceResourceId { get; set; } = string.Empty;

    /// <summary>Çizimde kullanılan resim: saydamlık kapalıysa orijinalin kendisi, açıksa işlenmiş PNG.</summary>
    public string RenderResourceId { get; set; } = string.Empty;

    public bool TransparencyEnabled { get; set; }

    /// <summary>Saydam yapılacak renk (ARGB).</summary>
    public uint TransparentColor { get; set; } = 0xFFFFFFFF;

    /// <summary>Hassasiyet 0–100: 0 yalnızca aynı renk, büyüdükçe yakın renkler de silinir.</summary>
    public double Tolerance { get; set; } = DefaultTolerance;

    public Rect2D Bounds => new(X, Y, Width, Height);
    public override Point2D Anchor => new(X, Y);

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() => new ImageObject
    {
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        SourceResourceId = SourceResourceId,
        RenderResourceId = RenderResourceId,
        TransparencyEnabled = TransparencyEnabled,
        TransparentColor = TransparentColor,
        Tolerance = Tolerance,
    };

    /// <summary>Resim ayarlarının tamamı (tek geri alma adımı için).</summary>
    [JsonIgnore]
    public ImageSettings Settings
    {
        get => new(RenderResourceId, TransparencyEnabled, TransparentColor, Tolerance);
        set => (RenderResourceId, TransparencyEnabled, TransparentColor, Tolerance) =
            (value.RenderResourceId, value.TransparencyEnabled, value.TransparentColor, value.Tolerance);
    }
}

public readonly record struct ImageSettings(string RenderResourceId, bool TransparencyEnabled, uint TransparentColor, double Tolerance);
