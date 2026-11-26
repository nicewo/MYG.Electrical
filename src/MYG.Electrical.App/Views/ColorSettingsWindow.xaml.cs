using System.Windows;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class ColorSettingsWindow : Window
{
    private readonly ColorSettingsViewModel _vm;

    public ColorSettingsWindow(ColorSettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var error = _vm.Apply();
        if (error is not null)
        {
            ErrorText.Text = error;
            return;
        }
        DialogResult = true;
    }
}
