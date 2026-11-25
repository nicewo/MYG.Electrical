namespace MYG.Electrical.Core.Model;

/// <summary>Pinin elektriksel fonksiyonu (DRC, potansiyel ve raporlar için).</summary>
public enum PinFunction
{
    /// <summary>Genel bağlantı (belirtilmemiş).</summary>
    General,

    /// <summary>Güç devresi ana bağlantısı (L1/T1, U/V/W...).</summary>
    Power,

    /// <summary>Bobin uçları (A1, A2).</summary>
    Coil,

    /// <summary>Kapanan kontak ucu.</summary>
    ContactNo,

    /// <summary>Açan kontak ucu.</summary>
    ContactNc,

    /// <summary>Değiştirici kontağın ortak ucu.</summary>
    ContactCommon,

    /// <summary>Besleme + (L+, +24V).</summary>
    SupplyPositive,

    /// <summary>Besleme − / 0V (M, 0V).</summary>
    SupplyNegative,

    /// <summary>Nötr.</summary>
    Neutral,

    /// <summary>Koruma topraklaması.</summary>
    ProtectiveEarth,

    /// <summary>Dijital/analog giriş (PLC girişi, sürücü girişi).</summary>
    Input,

    /// <summary>Dijital/analog çıkış (PLC çıkışı, sensör çıkışı).</summary>
    Output,

    /// <summary>Haberleşme (RS485, Ethernet, CAN...).</summary>
    Communication,

    /// <summary>Kablo ekranı.</summary>
    Shield,

    /// <summary>Klemens / konnektör geçiş ucu.</summary>
    Terminal,
}
