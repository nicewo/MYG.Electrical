using System.Windows;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class PinNameWindow : Window
{
    private readonly PinNameViewModel _vm;

    public PinNameWindow(PinNameViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.Name))
        {
            NameBox.Focus();
            return;
        }
        DialogResult = true;
    }
}
