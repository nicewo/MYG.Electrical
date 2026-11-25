using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.Core.Imaging;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Drawing;

namespace MYG.Electrical.App.ViewModels;

/// <summary>
/// Resim ekle / resim ayarları penceresi: boyut, saydamlık (renk seçici + hassasiyet) ve canlı önizleme.
/// Orijinal veri hiç değişmez; Tamam'a basılınca saydamlık tam çözünürlükte uygulanır.
/// </summary>
public sealed class ImageDialogViewModel : ObservableObject
{
    private const int PreviewMaxPixels = 900;
    private const double DefaultWidthMm = 80;
    private const double MaxDefaultWidthMm = 150;
    private const double MmPerInch = 25.4;
    private const double MinSizeMm = 1;

    private readonly DecodedImage _full;
    private readonly DecodedImage _preview;
    private readonly WriteableBitmap _previewBitmap;
    private readonly double _aspect; // yükseklik / genişlik

    private bool _transparencyEnabled;
    private uint _transparentColor;
    private double _tolerance;
    private double _widthMm;
    private double _heightMm;
    private bool _keepAspect = true;
    private string _transparentInfo = string.Empty;

    public ImageDialogViewModel(byte[] sourceData, string fileName, ImageObject? existing = null)
    {
        SourceData = sourceData;
        FileName = fileName;
        var bitmap = WpfImageCodec.LoadBitmap(sourceData);
        _full = WpfImageCodec.FromBitmap(bitmap);
        _preview = WpfImageCodec.Downscale(bitmap, PreviewMaxPixels);
        _previewBitmap = new WriteableBitmap(_preview.Width, _preview.Height, 96, 96, PixelFormats.Bgra32, null);
        _aspect = (double)_full.Height / _full.Width;

        if (existing is not null)
        {
            _transparencyEnabled = existing.TransparencyEnabled;
            _transparentColor = existing.TransparentColor;
            _tolerance = existing.Tolerance;
            _widthMm = existing.Width;
            _heightMm = existing.Height;
            _keepAspect = Math.Abs(existing.Height / Math.Max(existing.Width, MinSizeMm) - _aspect) < 0.01;
        }
        else
        {
            _transparentColor = _full.PixelAt(0, 0); // sol üst köşe genelde arka plan rengidir
            _tolerance = ImageObject.DefaultTolerance;
            var physicalWidth = bitmap.DpiX > 0 ? _full.Width / bitmap.DpiX * MmPerInch : DefaultWidthMm;
            _widthMm = Math.Round(Math.Clamp(physicalWidth, MinSizeMm * 10, MaxDefaultWidthMm));
            _heightMm = Math.Round(_widthMm * _aspect, 1);
        }
        UpdatePreview();
    }

    public byte[] SourceData { get; }
    public string FileName { get; }
    public string PixelSizeText => string.Create(CultureInfo.CurrentCulture, $"{_full.Width} × {_full.Height} piksel");
    public ImageSource Preview => _previewBitmap;

    public bool TransparencyEnabled
    {
        get => _transparencyEnabled;
        set
        {
            if (SetField(ref _transparencyEnabled, value))
            {
                UpdatePreview();
            }
        }
    }

    public uint TransparentColor
    {
        get => _transparentColor;
        private set
        {
            if (SetField(ref _transparentColor, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
                OnPropertyChanged(nameof(ColorText));
                UpdatePreview();
            }
        }
    }

    public Brush ColorBrush
    {
        get
        {
            var b = new SolidColorBrush(WpfConvert.ToColor(_transparentColor | 0xFF000000));
            b.Freeze();
            return b;
        }
    }

    public string ColorText => string.Create(CultureInfo.InvariantCulture,
        $"#{(_transparentColor >> 16) & 0xFF:X2}{(_transparentColor >> 8) & 0xFF:X2}{_transparentColor & 0xFF:X2}");

    /// <summary>Hassasiyet 0–100.</summary>
    public double Tolerance
    {
        get => _tolerance;
        set
        {
            var v = Math.Round(Math.Clamp(value, ColorKey.MinTolerance, ColorKey.MaxTolerance));
            if (SetField(ref _tolerance, v))
            {
                UpdatePreview();
            }
        }
    }

    public string TransparentInfo
    {
        get => _transparentInfo;
        private set => SetField(ref _transparentInfo, value);
    }

    public bool KeepAspect
    {
        get => _keepAspect;
        set
        {
            if (SetField(ref _keepAspect, value) && value)
            {
                HeightMm = _widthMm * _aspect;
            }
        }
    }

    public double WidthMm
    {
        get => _widthMm;
        set
        {
            if (SetField(ref _widthMm, Math.Max(MinSizeMm, value)) && _keepAspect)
            {
                _heightMm = Math.Round(_widthMm * _aspect, 2);
                OnPropertyChanged(nameof(HeightMm));
            }
        }
    }

    public double HeightMm
    {
        get => _heightMm;
        set
        {
            if (SetField(ref _heightMm, Math.Max(MinSizeMm, value)) && _keepAspect)
            {
                _widthMm = Math.Round(_heightMm / _aspect, 2);
                OnPropertyChanged(nameof(WidthMm));
            }
        }
    }

    /// <summary>Renk seçici: önizlemede tıklanan noktanın (0–1 göreli konum) rengini orijinal resimden alır.</summary>
    public void PickColor(double relativeX, double relativeY)
    {
        if (!TransparencyEnabled)
        {
            return;
        }
        var x = Math.Clamp((int)(relativeX * _full.Width), 0, _full.Width - 1);
        var y = Math.Clamp((int)(relativeY * _full.Height), 0, _full.Height - 1);
        TransparentColor = _full.PixelAt(x, y);
    }

    /// <summary>Tamam: çizimde kullanılacak veri (saydamlık açıksa tam çözünürlükte işlenmiş PNG).</summary>
    public byte[] BuildRenderData() => TransparencyEnabled
        ? ImageProcessor.ApplyTransparency(SourceData, TransparentColor, Tolerance, WpfImageCodec.Instance)
        : SourceData;

    private void UpdatePreview()
    {
        var pixels = (byte[])_preview.Bgra.Clone();
        if (TransparencyEnabled)
        {
            var removed = ColorKey.Apply(pixels, _transparentColor, _tolerance);
            var percent = 100.0 * removed / (_preview.Width * _preview.Height);
            TransparentInfo = string.Create(CultureInfo.CurrentCulture, $"Saydam: %{percent:0.#}");
        }
        else
        {
            TransparentInfo = string.Empty;
        }
        _previewBitmap.WritePixels(new Int32Rect(0, 0, _preview.Width, _preview.Height), pixels, _preview.Width * 4, 0);
    }
}
