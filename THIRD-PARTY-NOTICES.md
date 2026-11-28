# Üçüncü Taraf Bileşenler

MYG Electrical kurulum paketi (Setup.exe / Portable.zip) aşağıdaki üçüncü taraf bileşenleri içerir.
Her bileşen kendi lisansına tabidir; bu liste MYG Electrical'ın kendi lisansını belirlemez
(bkz. README → Lisans).

| Bileşen | Sürüm | Lisans | Kaynak |
|---|---|---|---|
| .NET Runtime, Windows Desktop (WPF) | 10.0 | MIT | https://github.com/dotnet/runtime, https://github.com/dotnet/wpf |
| PDFsharp (PdfSharp ve yardımcı derlemeleri) | 6.2.4 | MIT | https://github.com/empira/PDFsharp |
| Dirkster.AvalonDock, .Core, .Themes.VS, .Themes.VS2013, .Serializer.Xml (yerleştirilebilir paneller) | 5.1.0 | Ms-PL | https://github.com/Dirkster99/AvalonDock |
| DocumentFormat.OpenXml, DocumentFormat.OpenXml.Framework (Open XML SDK, rapor .xlsx çıktısı) | 3.5.1 | MIT | https://github.com/dotnet/Open-XML-SDK |
| Serilog | 4.4.0 | Apache-2.0 | https://github.com/serilog/serilog |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | https://github.com/serilog/serilog-sinks-file |
| Velopack | 1.2.161 | MIT | https://github.com/velopack/velopack |
| Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions | 10.x | MIT | https://github.com/dotnet/runtime |
| System.Security.Cryptography.Pkcs | 10.0.15 | MIT | https://github.com/dotnet/runtime |
| System.IO.Packaging | 10.0.x | MIT | https://github.com/dotnet/runtime |

Yalnızca geliştirme ve testte kullanılan, kurulum paketine girmeyen bileşenler:

| Bileşen | Lisans | Kaynak |
|---|---|---|
| xUnit v3 | Apache-2.0 | https://github.com/xunit/xunit |
| vpk (Velopack paketleme aracı, CI) | MIT | https://github.com/velopack/velopack |

Notlar:

- **PDFsharp 6.5 ve sonrası AGPL-3.0 lisanslıdır.** Proje bilinçli olarak MIT lisanslı 6.2 serisine
  sabitlenmiştir (`Directory.Packages.props`). Sürüm yükseltilirken lisans yeniden değerlendirilmelidir.
- Yazı tipleri pakete dahil değildir; ekran ve PDF çıktısı Windows'un kendi Arial yazı tipini kullanır.
- MYG Electrical ve MYG Tech logoları üçüncü taraf bileşen değildir; MYG Tech'e aittir.

Lisans metinleri:
[MIT](https://opensource.org/license/mit) ·
[Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0)
