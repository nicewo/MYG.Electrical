using System.Windows;
using Microsoft.Win32;
using MYG.Electrical.App.ViewModels;
using MYG.Electrical.Drawing;

namespace MYG.Electrical.App.Views;

public partial class DevicePropertiesWindow : Window
{
    private readonly DevicePropertiesViewModel _vm;
    private readonly Action<string> _open;

    public DevicePropertiesWindow(DevicePropertiesViewModel vm, Core.Symbols.ISymbolCatalog catalog, Action<string> open)
    {
        InitializeComponent();
        _vm = vm;
        _open = open;
        DataContext = vm;
        PreviewImage.Source = SymbolPreview.Create(vm.Symbol, catalog);
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        // Odaktaki hücre/metin kutusu değeri yazılsın
        if (System.Windows.Input.Keyboard.FocusedElement is FrameworkElement fe)
        {
            fe.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
        }
        var error = _vm.Apply();
        if (error is not null)
        {
            ErrorText.Text = error;
            return;
        }
        DialogResult = true;
    }

    private void OnBrowseDatasheet(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Dokümanlar (*.pdf;*.doc;*.docx;*.htm;*.html)|*.pdf;*.doc;*.docx;*.htm;*.html|Tüm dosyalar|*.*", Title = "Datasheet seç" };
        if (dlg.ShowDialog(this) == true)
        {
            _vm.Datasheet = dlg.FileName;
            DataContext = null;
            DataContext = _vm;
        }
    }

    private void OnOpenDatasheet(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_vm.Datasheet))
        {
            _open(_vm.Datasheet);
        }
    }
}
