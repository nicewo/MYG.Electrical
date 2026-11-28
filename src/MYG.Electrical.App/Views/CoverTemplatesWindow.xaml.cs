using System.Windows;
using System.Windows.Input;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class CoverTemplatesWindow : Window
{
    private readonly CoverTemplatesViewModel _vm;

    public CoverTemplatesWindow(CoverTemplatesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.ApplyCommand.CanExecute(null))
        {
            _vm.ApplyCommand.Execute(null);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
