# MYG Electrical Kullanım Kılavuzu

Bu kılavuz programın **şu anki sürümünde çalışan** özelliklerini anlatır. Planlanan özellikler için
[Yol haritası](Roadmap.md).

## 1. Kurulum ve güncelleme

1. [Son sürüm](https://github.com/nicewo/MYG.Electrical/releases/latest) sayfasından
   `MYG.Electrical-win-Setup.exe` dosyasını indirip çalıştırın. Yönetici izni gerekmez; program
   `%LocalAppData%\MYG.Electrical` altına kurulur, Başlat menüsüne ve masaüstüne kısayol eklenir.
2. Program imzasız olduğu için Windows SmartScreen uyarı verebilir: **Ek bilgi → Yine de çalıştır**.
3. Program her açılışta internet varsa yeni sürümü denetler, arka planda indirir ve
   **şimdi yeniden başlatılsın mı?** diye sorar. *Hayır* derseniz program kapanınca kurulur.
   Elle denetleme: **Yardım → Güncellemeleri denetle**. Kullanılan sürüm sağ alttaki durum çubuğunda yazar.

## 2. Ekran düzeni

| Bölüm | İçerik |
|---|---|
| Üst menü | Dosya, Düzen, Görünüm, Ekle, Çizim, Sayfa, Araçlar, Yardım |
| Araç çubuğu | Seç, çizgi, dikdörtgen, metin, sembol, dirsek, T, çapraz, resim, sil, döndür, geri/ileri al, yakınlaştırma, grid, snap |
| Sol panel | Proje ağacı, sayfalar, IEC sembol kütüphanesi (aranabilir) |
| Orta | Çizim alanı: beyaz sayfa, grid, antet |
| Sağ panel | Seçili nesnenin özellikleri; seçim yoksa sayfa ve proje (kapak/antet) bilgileri |
| Durum çubuğu | İmleç koordinatı (mm), zoom, grid/snap, kablo sayısı, aktif sayfa, sürüm |

## 3. Proje ve sayfalar

- **Yeni proje** (Ctrl+N) bir kapak ve bir şema sayfasıyla başlar.
- **Sayfa ekleme:** Sayfa menüsü veya sol paneldeki `+`. Kapak sayfası başa eklenir.
- **Sıra, silme:** Sol paneldeki ▲ ▼ ✕ düğmeleri. Sayfa silme dahil bütün sayfa işlemleri Ctrl+Z ile geri alınır.
- **Sayfa ayarları:** Çizim alanında boş bir yere tıklayın (seçim olmasın); sağ panelde sayfa adı, türü
  (kapak/şema), formatı (A4–A1) ve yönelimi (yatay/dikey) değiştirilir.
- **Kapak ve antet:** Aynı panelde proje adı, firma, makine adı, proje no, müşteri, hazırlayan, kontrol eden,
  tarih, revizyon ve logo. Bu bilgiler kapak sayfasına ve her sayfanın antedine otomatik yazılır.
- **Örnek proje:** Yardım → Örnek projeyi aç. Örnek, kaydedilmemiş yeni proje olarak açılır.

## 4. Çizim

- **Grid ve snap:** 5 mm grid; Görünüm menüsünden veya araç çubuğundan açılıp kapatılır.
- **Çizgi / dikdörtgen:** Aracı seçin, başlangıç ve bitiş noktasına tıklayın.
- **Metin:** T ile tıklayın; içeriği ve yüksekliği sağ panelden düzenleyin.
- **Seçme ve taşıma:** Tıklayın veya alan seçin; sürükleyin ya da ok tuşlarını kullanın.
- **Döndürme:** R (90° adımlar). Sağ tık menüsünde de vardır.
- **Kopyala / yapıştır:** Yapıştırılan cihazlar yeni tanım numarası alır (-K1 → -K2).

## 5. Semboller

Sol paneldeki **Sembol kütüphanesi**nden bir sembol seçin, çizim alanında tıklayarak yerleştirin
(yerleştirmeden önce R ile döndürebilirsiniz). Sağ tık veya Esc ile bitirin.

Mevcut IEC sembolleri: kontaktör (3 kutup), otomatik sigorta (3 kutup), sigorta, motor (3 faz), bobin,
röle (bobin + NO), açık kontak (NO), kapalı kontak (NC), buton (NO), acil stop (NC), limit switch (NO),
endüktif sensör (PNP), sinyal lambası, klemens.

Her cihaz otomatik tanım alır (`-K1`, `-Q1`, `-M1`…); sağ panelden değiştirilebilir. Pin numaraları
(1-2, A1-A2, 13-14, U1-V1-W1…) çizimde ve PDF'te görünür.

## 6. Otomatik kablolama

Kablolar elle çizilmez. İki pin **aynı yatay veya dikey hatta** ve **birbirine bakıyorsa** otomatik bağlanır;
arada daha yakın uygun bir pin varsa uzaktakine bağlanmaz. Cihaz taşındıkça kablolar anında güncellenir.

Kablonun dönmesi veya dallanması için **bağlantı noktaları** kullanılır:

| Araç | Kısayol | Ne işe yarar |
|---|---|---|
| Dirsek | D | Kablonun 90° dönmesi (┌ ┐ └ ┘). Noktası çizilmez. |
| T bağlantı | J | Bir hattan dal almak (┬ ┤ ┴ ├). Kapalı tarafından gelen hat bağlanmaz. |
| Çapraz | araç çubuğu | Kesişen yatay ve dikey hattı gerçekten birleştirmek için (┼). |

Dirsek veya T yerleştirirken program, çevredeki pinlere bakarak uygun yönü **otomatik önerir**; R ile
değiştirebilirsiniz. Bağlantı noktası olmayan kesişmelerde hatlar birbirine bağlanmaz.

## 7. Resim ekleme

**Ekle → Resim…** veya araç çubuğundaki resim düğmesi:

1. Dosyayı seçin (PNG, JPG, BMP, GIF, TIFF). Resim **projenin içine kopyalanır**; orijinal dosya
   silinse de proje bozulmaz.
2. Boyutu mm olarak girin (en-boy oranı korunabilir).
3. **Saydamlık** kutusunu işaretleyip önizlemede saydam olacak renge tıklayın. **Hassasiyet** (0–100)
   arttıkça o renge yakın tonlar da silinir. Kenarlarda açık renk hale kalırsa 15–25 deneyin.
4. Tamam'dan sonra sayfada resmin sol üst köşesine tıklayın.

Sonradan değiştirmek için resmi seçip sağ paneldeki **Resim ayarları…** düğmesine basın.

## 8. Kaydetme ve açma

- Proje tek bir `.myge` dosyasıdır (resimler dahil).
- Her kayıtta önceki sürüm `<dosya>.myge.bak` olarak saklanır. Kayıt herhangi bir nedenle yarım kalırsa
  mevcut dosya değişmez.
- Açılışta onarılan sorunlar (ör. eksik resim) bir liste halinde gösterilir.

## 9. PDF çıktısı

- **Dosya → Tüm projeyi PDF'e aktar** (Ctrl+P) veya **Aktif sayfayı PDF'e aktar**.
- Çıktı vektöreldir, sayfalar gerçek ölçüsünde (A4–A1) ve kapak/antetle birlikte basılır.
- Sayfa sınırının dışına taşan nesneler için uyarı verilir.

## 10. Sorun giderme

- **Loglar:** Araçlar → Log klasörünü aç (`%LocalAppData%\MYG.Electrical\logs`).
- **Hata bildirimi ve öneriler:** Yardım → Hata bildir / öneri (GitHub Issues). Mümkünse son log dosyasını ekleyin.
