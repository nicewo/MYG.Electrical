using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>Tüm çizim nesnelerinin temel sınıfı. Ölçü birimi mm'dir.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LineObject), "line")]
[JsonDerivedType(typeof(RectangleObject), "rectangle")]
[JsonDerivedType(typeof(TextObject), "text")]
[JsonDerivedType(typeof(SymbolObject), "symbol")]
[JsonDerivedType(typeof(DeviceObject), "device")]
[JsonDerivedType(typeof(JunctionObject), "junction")]
[JsonDerivedType(typeof(ImageObject), "image")]
[JsonDerivedType(typeof(PotentialObject), "potential")]
[JsonDerivedType(typeof(PageLinkObject), "pageLink")]
[JsonDerivedType(typeof(DevicePinObject), "devicePin")]
[JsonDerivedType(typeof(FieldObject), "field")]
public abstract class DrawingObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nesneyi verilen vektör kadar taşır.</summary>
    public abstract void Move(double dx, double dy);

    /// <summary>Kopyalama için yeni kimlikli derin kopya üretir.</summary>
    public abstract DrawingObject CloneWithNewId();

    /// <summary>Taşıma/döndürme referans noktası.</summary>
    public abstract Point2D Anchor { get; }

    /// <summary>Sürüklerken grid'e oturtulan nokta (sembolde ekleme noktası, çizgide başlangıç, dikdörtgende sol üst).</summary>
    public virtual Point2D SnapPoint => Anchor;

    /// <summary>Bu nesne otomatik kablolamayı etkiler mi?</summary>
    [JsonIgnore]
    public virtual bool AffectsWiring => false;
}

/// <summary>Döndürülebilen nesneler.</summary>
public interface IRotatable
{
    double Rotation { get; set; }
}
