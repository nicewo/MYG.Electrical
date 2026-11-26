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
        Canvas.ObjectDoubleClicked += (_, obj) =>
        {
            if (obj is Core.Model.DeviceObject device)
            {
                vm.OpenDeviceProperties(device);
            }
        };
        Canvas.AllowDrop = true;
        Canvas.DragOver += OnCanvasDragOver;
        Canvas.Drop += OnCanvasDrop;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Canvas.Focus();
    }

    // ICanvasController
    public void ZoomIn() => Canvas.ZoomIn();
    public void ZoomOut() => Canvas.ZoomOut();
    public void ZoomToFit() => Canvas.ZoomToFit();
    public void RefreshAll() => Canvas.RefreshAll();
    public void RotatePlacement() => Canvas.RotatePlacement();

    public void InvalidateFileImages()
    {
        MYG.Electrical.Drawing.WpfSceneSink.ClearImageCache();
        Canvas.RefreshAll();
    }

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

    // ---------- Kütüphaneden sürükle-bırak ----------

    public const string LibraryDragFormat = "MYG.Electrical.LibraryItem";
    private Point? _libraryDragStart;

    private void OnLibraryMouseDown(object sender, MouseButtonEventArgs e)
    {
        _libraryDragStart = e.GetPosition(this);
        if (e.ClickCount == 2 && (e.OriginalSource as FrameworkElement)?.DataContext is LibraryItemViewModel item)
        {
            _vm.Library.Place(item);
            Canvas.Focus();
            e.Handled = true;
        }
    }

    private void OnLibraryMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _libraryDragStart is not { } start
            || (e.OriginalSource as FrameworkElement)?.DataContext is not LibraryItemViewModel item)
        {
            return;
        }
        var d = e.GetPosition(this) - start;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }
        _libraryDragStart = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(LibraryDragFormat, item), DragDropEffects.Copy);
    }

    private void OnLibrarySelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        _vm.Library.Selected = e.NewValue as LibraryItemViewModel;

    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(LibraryDragFormat) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCanvasDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(LibraryDragFormat) is LibraryItemViewModel item)
        {
            _vm.DropLibraryItem(item, Canvas.ScreenToWorld(e.GetPosition(Canvas)));
            Canvas.Focus();
            e.Handled = true;
        }
    }

    private void OnFindingDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _vm.BottomPanel.GoToFindingCommand.Execute(null);
        Canvas.Focus();
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
