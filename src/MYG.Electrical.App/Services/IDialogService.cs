namespace MYG.Electrical.App.Services;

public interface IDialogService
{
    string? OpenProjectFile();
    string? SaveProjectFile(string suggestedName);
    string? SavePdfFile(string suggestedName);
    string? OpenImageFile();

    /// <summary>Resim ekle/ayarla penceresi. Tamam → true.</summary>
    bool ShowImageDialog(ViewModels.ImageDialogViewModel vm);

    /// <summary>Kaydedilmemiş değişiklikler için Evet/Hayır/İptal. İptal → null.</summary>
    bool? ConfirmSaveChanges(string projectName);

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
}
