using System.Windows;
using System.Windows.Input;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class ImageDialog : Window
{
    private readonly ImageDialogViewModel _vm;

    public ImageDialog(ImageDialogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    /// <summary>Renk seçici: tıklanan noktayı resmin göreli koordinatına çevirir.</summary>
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (PreviewImage.ActualWidth <= 0 || PreviewImage.ActualHeight <= 0)
        {
            return;
        }
        var p = e.GetPosition(PreviewImage);
        _vm.PickColor(p.X / PreviewImage.ActualWidth, p.Y / PreviewImage.ActualHeight);
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        // Odaktaki metin kutusunun değeri işlensin
        (Keyboard.FocusedElement as FrameworkElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        DialogResult = true;
    }
}
