using System.Windows;
using Microsoft.Win32;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.App.Services;

public sealed class DialogService : IDialogService
{
    private const string AppTitle = "MYG Electrical";
    private static readonly string ProjectFilter = $"MYG Electrical Projesi (*{ProjectDocument.FileExtension})|*{ProjectDocument.FileExtension}";

    public string? OpenProjectFile()
    {
        var dlg = new OpenFileDialog { Filter = ProjectFilter, Title = "Proje Aç" };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public string? SaveProjectFile(string suggestedName)
    {
        var dlg = new SaveFileDialog { Filter = ProjectFilter, FileName = Sanitize(suggestedName), Title = "Projeyi Kaydet", AddExtension = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public string? SavePdfFile(string suggestedName)
    {
        var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = Sanitize(suggestedName), Title = "PDF Olarak Kaydet", AddExtension = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public string? OpenImageFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Resim (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
            Title = "Resim Seç",
        };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public void ShowAbout(ViewModels.AboutViewModel vm) => new Views.AboutWindow(vm) { Owner = Owner }.ShowDialog();

    public void ShowShortcuts() => new Views.ShortcutsWindow { Owner = Owner }.ShowDialog();

    public bool ShowImageDialog(ViewModels.ImageDialogViewModel vm) =>
        new Views.ImageDialog(vm) { Owner = Owner }.ShowDialog() == true;

    public bool? ConfirmSaveChanges(string projectName)
    {
        var result = MessageBox.Show(Owner!, $"'{projectName}' projesinde kaydedilmemiş değişiklikler var.\nKaydetmek ister misiniz?",
            AppTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null,
        };
    }

    public bool Confirm(string message) =>
        MessageBox.Show(Owner!, message, AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message) => MessageBox.Show(Owner!, message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) => MessageBox.Show(Owner!, message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);

    private static Window? Owner => Application.Current?.MainWindow;

    private static string Sanitize(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "Proje" : clean;
    }
}
