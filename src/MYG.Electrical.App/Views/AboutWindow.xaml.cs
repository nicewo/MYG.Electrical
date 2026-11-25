using System.Windows;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AboutViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
