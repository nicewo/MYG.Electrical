namespace MYG.Electrical.Core.Symbols;

/// <summary>Sembolün cihaz içindeki rolü (çok parçalı cihaz).</summary>
public enum SymbolRole
{
    /// <summary>Tek parçalı cihaz (sigorta, motor, sensör...). Etiketi projede tektir.</summary>
    Standalone,

    /// <summary>Ana fonksiyon (bobin, PLC CPU, sürücü...). Aynı etiketli kontaklar buna bağlanır.</summary>
    Main,

    /// <summary>Yardımcı parça (kontak, ana kontaklar, I/O kanalı). Etiketi ana fonksiyonunkiyle aynıdır.</summary>
    Contact,

    /// <summary>Elektrik cihazı olmayan işaret (topraklama işareti, kablo rakoru...). Etiket almaz.</summary>
    Graphic,
}

/// <summary>
/// İç bağlantının türü. Varsayılan (0) değer Switched'dır ve kayıt yapıcısının varsayılanıyla aynıdır:
/// JSON yazımında varsayılanlar atlandığı için sıra değiştirilmemelidir.
/// </summary>
public enum InternalConnectionKind
{
    /// <summary>Durumla değişen (kontak, kesici). Potansiyel adı/rengi geçer, kısa devre kontrolü geçmez.</summary>
    Switched,

    /// <summary>Her zaman iletken (klemens, köprü, sigorta, konnektör).</summary>
    Permanent,
}

/// <summary>Sembol içinde iki pin arasındaki iletken yol.</summary>
public sealed record InternalConnection(string A, string B, InternalConnectionKind Kind = InternalConnectionKind.Switched);

/// <summary>
/// Etiket bloğunun (etiket, işlev metni, çapraz referans) sembole göre yeri. Varsayılan (0) sağdır; JSON yazımında
/// varsayılanlar atlandığı için sıra değiştirilmemelidir.
/// </summary>
public enum ReferencePosition
{
    /// <summary>Sembolün sağında (yatay hatlardaki cihazlar).</summary>
    Right,

    /// <summary>Sembolün solunda (EPLAN'daki gibi dikey bobin, kontak, klemens).</summary>
    Left,

    /// <summary>Sembolün üstünde, ortalı (yan yana dizilen dar PLC modülleri).</summary>
    Top,

    /// <summary>
    /// Dışarıya yazılmaz: sembol kendi metin alanlarıyla gösterir (EPLAN PLC kutusu). Metin şekillerinde
    /// {Reference}, {Description}, {Pin:1}, {CrossReference} alanları yerleşimin değeriyle doldurulur.
    /// </summary>
    InSymbol,
}
