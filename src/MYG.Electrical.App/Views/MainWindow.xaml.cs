using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MYG.Electrical.App.Services;
using MYG.Electrical.App.ViewModels;

namespace MYG.Electrical.App.Views;

public partial class MainWindow : Window, ICanvasController
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.Canvas = this;
        Canvas.CursorMoved += (_, p) => vm.OnCursorMoved(p);
        Canvas.ViewChanged += (_, _) => vm.OnViewChanged(Canvas.View);
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Canvas.Focus();
    }

    // ICanvasController
    public void ZoomIn() => Canvas.ZoomIn();
    public void ZoomOut() => Canvas.ZoomOut();
    public void ZoomToFit() => Canvas.ZoomToFit();
    public void RefreshAll() => Canvas.RefreshAll();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_vm.ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is PageItemViewModel page)
        {
            _vm.SelectedPage = page;
        }
    }

    /// <summary>Özellik panelinde Enter değeri hemen uygular.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox box)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            box.SelectAll();
            e.Handled = true;
        }
    }
}
