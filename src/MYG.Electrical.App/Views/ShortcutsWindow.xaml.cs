using System.Windows;
using System.Windows.Data;

namespace MYG.Electrical.App.Views;

public partial class ShortcutsWindow : Window
{
    public ShortcutsWindow()
    {
        InitializeComponent();
        var view = CollectionViewSource.GetDefaultView(
            ShortcutCatalog.All.Select(s => new { s.Group, s.Keys, s.Action }).ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        List.ItemsSource = view;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
