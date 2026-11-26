using System.Windows;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class CreateDeviceWindow : Window
{
    private readonly CreateDeviceViewModel _vm;

    public CreateDeviceWindow(CreateDeviceViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Input.Keyboard.FocusedElement is FrameworkElement fe)
        {
            fe.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
        }
        var error = _vm.Validate();
        if (error is not null)
        {
            ErrorText.Text = error;
            return;
        }
        DialogResult = true;
    }
}
