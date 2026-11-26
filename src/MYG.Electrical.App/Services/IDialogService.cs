namespace MYG.Electrical.App.Services;

public interface IDialogService
{
    string? OpenProjectFile();
    string? SaveProjectFile(string suggestedName);
    string? SavePdfFile(string suggestedName);
    string? OpenImageFile();

    /// <summary>Resim ekle/ayarla penceresi. Tamam → true.</summary>
    bool ShowImageDialog(ViewModels.ImageDialogViewModel vm);

    void ShowAbout(ViewModels.AboutViewModel vm);

    /// <summary>Cihaz özellikleri penceresi. Tamam → true (değişiklikler uygulanmıştır).</summary>
    bool ShowDeviceProperties(ViewModels.DevicePropertiesViewModel vm, Core.Symbols.ISymbolCatalog catalog, Action<string> open);
    void ShowShortcuts();

    /// <summary>Kaydedilmemiş değişiklikler için Evet/Hayır/İptal. İptal → null.</summary>
    bool? ConfirmSaveChanges(string projectName);

    /// <summary>Evet/Hayır onayı (ör. dolu sayfayı silme).</summary>
    bool Confirm(string message);

    void ShowError(string message);
    void ShowInfo(string message);
}

/// <summary>Çizim alanına görünüm komutlarını iletmek için (View tarafından sağlanır).</summary>
public interface ICanvasController
{
    void ZoomIn();
    void ZoomOut();
    void ZoomToFit();
    void RefreshAll();

    /// <summary>Yerleştirme önizlemesini 90° döndürür.</summary>
    void RotatePlacement();

    /// <summary>Logo gibi dosyadan okunan resimler değişince önbelleği yeniler.</summary>
    void InvalidateFileImages();
}

/// <summary>Bağlantıları ve klasörleri işletim sisteminin varsayılan uygulamasıyla açar.</summary>
public interface IShellService
{
    void OpenUrl(string url);
}
