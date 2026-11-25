using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Drawing;

/// <summary>
/// DrawingVisual tabanlı teknik çizim alanı. Model <see cref="EditorSession"/> üzerinden okunur ve değiştirilir;
/// her nesne kendi görselinde tutulur ve yalnızca değişen nesneler yeniden çizilir.
/// </summary>
public sealed class DrawingCanvas : FrameworkElement
{
    private const double MinGridPixels = 6.0;
    private const double PageShadowMm = 1.5;
    private const double SelectionPaddingPixels = 3.0;
    private const double SnapMarkerPixels = 6.0;
    private const double GhostOpacity = 0.45;
    private const double MinRubberBandPixels = 3.0;

    private static readonly Brush PageBrush = Frozen(new SolidColorBrush(Colors.White));
    private static readonly Brush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0)));
    private static readonly Pen GridPenBase = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xE3, 0xE5, 0xE8))), 1));
    private static readonly Brush SelectionBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x2F, 0x80, 0xED)));
    private static readonly Brush ActiveToolBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xF2, 0x8C, 0x28)));
    private static readonly Brush RubberFill = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0x2F, 0x80, 0xED)));
    private static readonly Pen SelectionPen = Frozen(new Pen(SelectionBrush, 1) { DashStyle = DashStyles.Dash });
    private static readonly Pen RubberPen = Frozen(new Pen(SelectionBrush, 1));
    private static readonly Pen PreviewPen = Frozen(new Pen(ActiveToolBrush, 1.2) { DashStyle = DashStyles.Dash });
    private static readonly Pen SnapPen = Frozen(new Pen(ActiveToolBrush, 1));

    private readonly VisualCollection _visuals;
    private readonly ContainerVisual _world = new();
    private readonly DrawingVisual _pageVisual = new();
    private readonly DrawingVisual _frameVisual = new();
    private readonly DrawingVisual _wireVisual = new();
    private readonly ContainerVisual _objectLayer = new();
    private readonly DrawingVisual _ghostVisual = new() { Opacity = GhostOpacity };
    private readonly DrawingVisual _overlayVisual = new();
    private readonly Dictionary<Guid, DrawingVisual> _objectVisuals = [];

    private SceneComposer? _composer;
    private bool _needsFit = true;

    // Etkileşim durumu
    private bool _panning;
    private Point _lastPanPoint;
    private IReadOnlyList<DrawingObject>? _dragObjects;
    private Point2D _dragStart;
    private Point2D _dragApplied;
    private Point2D? _rubberStart;
    private bool _rubberAdditive;
    private Point2D? _shapeStart;
    private Point2D _cursorWorld;

    public DrawingCanvas()
    {
        _visuals = new VisualCollection(this) { _world, _overlayVisual };
        _world.Children.Add(_pageVisual);
        _world.Children.Add(_frameVisual);
        _world.Children.Add(_wireVisual);
        _world.Children.Add(_objectLayer);
        _world.Children.Add(_ghostVisual);
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        View.Changed += (_, _) => OnViewChanged();
        SizeChanged += (_, _) =>
        {
            if (_needsFit)
            {
                ZoomToFit();
            }
        };
    }

    public ViewTransform View { get; } = new();

    /// <summary>Fare imlecinin sayfa koordinatı (mm) değiştiğinde.</summary>
    public event EventHandler<Point2D>? CursorMoved;

    /// <summary>Zoom veya pan değiştiğinde.</summary>
    public event EventHandler? ViewChanged;

    // ---------- Bağımlılık özellikleri ----------

    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session), typeof(EditorSession), typeof(DrawingCanvas),
        new PropertyMetadata(null, (d, e) => ((DrawingCanvas)d).OnSessionChanged((EditorSession?)e.OldValue, (EditorSession?)e.NewValue)));

    public EditorSession? Session
    {
        get => (EditorSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public static readonly DependencyProperty ActiveToolProperty = DependencyProperty.Register(
        nameof(ActiveTool), typeof(EditorTool), typeof(DrawingCanvas),
        new FrameworkPropertyMetadata(EditorTool.Select, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((DrawingCanvas)d).OnToolChanged()));

    public EditorTool ActiveTool
    {
        get => (EditorTool)GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public static readonly DependencyProperty PendingSymbolProperty = DependencyProperty.Register(
        nameof(PendingSymbol), typeof(SymbolDefinition), typeof(DrawingCanvas),
        new PropertyMetadata(null, (d, _) => ((DrawingCanvas)d).RedrawGhost()));

    public SymbolDefinition? PendingSymbol
    {
        get => (SymbolDefinition?)GetValue(PendingSymbolProperty);
        set => SetValue(PendingSymbolProperty, value);
    }

    // ---------- Görünüm komutları ----------

    public void ZoomIn() => View.ZoomAt(ViewportCenter, ViewTransform.DefaultZoomStep);

    public void ZoomOut() => View.ZoomAt(ViewportCenter, 1 / ViewTransform.DefaultZoomStep);

    public void ZoomToFit()
    {
        if (Session is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        _needsFit = false;
        View.FitTo(Session.ActivePage.Bounds, ActualWidth, ActualHeight);
    }

    /// <summary>Tüm görselleri modelden yeniden üretir.</summary>
    public void RefreshAll()
    {
        _objectLayer.Children.Clear();
        _objectVisuals.Clear();
        if (Session is not null)
        {
            foreach (var obj in Session.ActivePage.Objects)
            {
                AddObjectVisual(obj);
            }
        }
        RedrawPage();
        RedrawFrame();
        RedrawWires();
        RedrawOverlay();
    }

    private Point2D ViewportCenter => new(ActualWidth / 2, ActualHeight / 2);

    // ---------- Visual ağacı ----------

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    /// <summary>Boş alanlarda da fare olaylarını almak için tüm yüzey isabet kabul edilir.</summary>
    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
        new PointHitTestResult(this, hitTestParameters.HitPoint);

    // ---------- Oturum olayları ----------

    private void OnSessionChanged(EditorSession? oldSession, EditorSession? newSession)
    {
        if (oldSession is not null)
        {
            oldSession.SceneChanged -= OnSceneChanged;
            oldSession.SelectionChanged -= OnSelectionChanged;
            oldSession.WiresChanged -= OnWiresChanged;
            oldSession.ActivePageChanged -= OnActivePageChanged;
        }
        _composer = newSession is null ? null : new SceneComposer(newSession.Catalog);
        if (newSession is not null)
        {
            newSession.SceneChanged += OnSceneChanged;
            newSession.SelectionChanged += OnSelectionChanged;
            newSession.WiresChanged += OnWiresChanged;
            newSession.ActivePageChanged += OnActivePageChanged;
        }
        CancelOperation(resetTool: false);
        RefreshAll();
        ZoomToFit();
        _needsFit = ActualWidth <= 0;
    }

    private void OnActivePageChanged(object? sender, EventArgs e)
    {
        CancelOperation(resetTool: false);
        RefreshAll();
        ZoomToFit();
    }

    private void OnSceneChanged(object? sender, SceneChangedEventArgs e)
    {
        if (Session is null)
        {
            return;
        }
        if (e.IsFullRefresh)
        {
            RefreshAll();
            return;
        }
        foreach (var id in e.ChangedIds!)
        {
            var obj = Session.ActivePage.Find(id);
            if (obj is null)
            {
                if (_objectVisuals.Remove(id, out var stale))
                {
                    _objectLayer.Children.Remove(stale);
                }
            }
            else if (_objectVisuals.TryGetValue(id, out var visual))
            {
                RenderObject(visual, obj);
            }
            else
            {
                AddObjectVisual(obj);
            }
        }
        RedrawOverlay();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => RedrawOverlay();

    private void OnWiresChanged(object? sender, EventArgs e) => RedrawWires();

    private void OnViewChanged()
    {
        _world.Transform = new MatrixTransform(WpfConvert.ToMatrix(View));
        RedrawPage();
        RedrawOverlay();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnToolChanged()
    {
        _shapeStart = null;
        Cursor = ActiveTool == EditorTool.Select ? Cursors.Arrow : Cursors.Cross;
        RedrawGhost();
        RedrawOverlay();
    }

    // ---------- Çizim ----------

    private void AddObjectVisual(DrawingObject obj)
    {
        var visual = new DrawingVisual();
        RenderObject(visual, obj);
        _objectVisuals[obj.Id] = visual;
        _objectLayer.Children.Add(visual);
    }

    private void RenderObject(DrawingVisual visual, DrawingObject obj)
    {
        using var dc = visual.RenderOpen();
        _composer?.ComposeObject(obj, new WpfSceneSink(dc), EditorOptions);
    }

    private SceneComposerOptions EditorOptions =>
        Session?.Settings.ShowConnectionPoints == false ? SceneComposerOptions.Print : SceneComposerOptions.Editor;

    private void RedrawPage()
    {
        using var dc = _pageVisual.RenderOpen();
        if (Session is null)
        {
            return;
        }
        var page = Session.ActivePage.Bounds;
        dc.DrawRectangle(ShadowBrush, null, WpfConvert.ToRect(new Rect2D(page.X + PageShadowMm, page.Y + PageShadowMm, page.Width, page.Height)));
        dc.DrawRectangle(PageBrush, null, WpfConvert.ToRect(page));

        if (!Session.Settings.GridVisible || Session.Settings.GridSpacing <= 0)
        {
            return;
        }
        var spacing = Session.Settings.GridSpacing;
        while (spacing * View.Scale < MinGridPixels)
        {
            spacing *= 2;
        }
        var pen = GridPenBase.CloneCurrentValue();
        pen.Thickness = 1 / View.Scale; // her zoom seviyesinde 1 piksel
        pen.Freeze();
        for (var x = spacing; x < page.Width; x += spacing)
        {
            dc.DrawLine(pen, new Point(x, 0), new Point(x, page.Height));
        }
        for (var y = spacing; y < page.Height; y += spacing)
        {
            dc.DrawLine(pen, new Point(0, y), new Point(page.Width, y));
        }
    }

    private void RedrawFrame()
    {
        using var dc = _frameVisual.RenderOpen();
        if (Session is null)
        {
            return;
        }
        var doc = Session.Document;
        var page = Session.ActivePage;
        PageFrameComposer.Compose(doc.Project, page, doc.Pages.IndexOf(page) + 1, doc.Pages.Count, new WpfSceneSink(dc));
    }

    private void RedrawWires()
    {
        using var dc = _wireVisual.RenderOpen();
        if (Session is not null)
        {
            SceneComposer.ComposeWires(Session.Wires, new WpfSceneSink(dc));
        }
    }

    private void RedrawGhost()
    {
        using var dc = _ghostVisual.RenderOpen();
        if (Session is null || _composer is null || ActiveTool != EditorTool.Symbol || PendingSymbol is null || !IsMouseOver)
        {
            return;
        }
        var p = Session.Snap(_cursorWorld);
        var ghost = PendingSymbol.CreateDevice(p.X, p.Y, string.Empty);
        _composer.ComposeObject(ghost, new WpfSceneSink(dc), SceneComposerOptions.Editor);
    }

    /// <summary>Seçim çerçeveleri, seçim dikdörtgeni ve araç önizlemeleri (ekran koordinatlarında).</summary>
    private void RedrawOverlay()
    {
        using var dc = _overlayVisual.RenderOpen();
        if (Session is null)
        {
            return;
        }

        foreach (var obj in Session.SelectedObjects)
        {
            var b = ObjectBounds.Get(obj, Session.Catalog);
            dc.DrawRectangle(null, SelectionPen, ScreenRect(b, SelectionPaddingPixels));
        }

        if (_rubberStart is { } rs)
        {
            dc.DrawRectangle(RubberFill, RubberPen, ScreenRect(Rect2D.FromPoints(rs, _cursorWorld), 0));
        }

        if (ActiveTool is (EditorTool.Line or EditorTool.Rectangle or EditorTool.Text or EditorTool.Junction or EditorTool.Symbol) && IsMouseOver)
        {
            var snapped = Session.Snap(_cursorWorld);
            var s = ToScreen(snapped);
            dc.DrawLine(SnapPen, new Point(s.X - SnapMarkerPixels, s.Y), new Point(s.X + SnapMarkerPixels, s.Y));
            dc.DrawLine(SnapPen, new Point(s.X, s.Y - SnapMarkerPixels), new Point(s.X, s.Y + SnapMarkerPixels));

            if (_shapeStart is { } start)
            {
                if (ActiveTool == EditorTool.Line)
                {
                    dc.DrawLine(PreviewPen, ToScreen(start), s);
                }
                else if (ActiveTool == EditorTool.Rectangle)
                {
                    dc.DrawRectangle(null, PreviewPen, ScreenRect(Rect2D.FromPoints(start, snapped), 0));
                }
            }
        }
    }

    private Point ToScreen(Point2D world) => WpfConvert.ToPoint(View.WorldToScreen(world));

    private Rect ScreenRect(Rect2D world, double padding)
    {
        var a = View.WorldToScreen(new Point2D(world.Left, world.Top));
        var b = View.WorldToScreen(new Point2D(world.Right, world.Bottom));
        return new Rect(a.X - padding, a.Y - padding, b.X - a.X + 2 * padding, b.Y - a.Y + 2 * padding);
    }

    // ---------- Fare ve klavye ----------

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ViewTransform.DefaultZoomStep : 1 / ViewTransform.DefaultZoomStep;
        View.ZoomAt(WpfConvert.ToPoint2D(e.GetPosition(this)), factor);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        if (Session is null)
        {
            return;
        }
        var screen = e.GetPosition(this);
        var world = View.ScreenToWorld(WpfConvert.ToPoint2D(screen));
        _cursorWorld = world;

        switch (e.ChangedButton)
        {
            case MouseButton.Middle:
                _panning = true;
                _lastPanPoint = screen;
                Cursor = Cursors.SizeAll;
                CaptureMouse();
                break;
            case MouseButton.Right:
                CancelOperation(resetTool: true);
                break;
            case MouseButton.Left:
                OnLeftDown(world, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                break;
        }
        e.Handled = true;
    }

    private void OnLeftDown(Point2D world, bool ctrl)
    {
        var session = Session!;
        var snapped = session.Snap(world);
        switch (ActiveTool)
        {
            case EditorTool.Select:
                var hit = session.HitTest(world, View.ScreenLengthToWorld(GeometryConstants.HitTolerancePixels));
                if (hit is not null)
                {
                    if (ctrl)
                    {
                        session.Select(hit, additive: true);
                        return;
                    }
                    if (!session.IsSelected(hit.Id))
                    {
                        session.Select(hit);
                    }
                    _dragObjects = session.SelectedObjects;
                    _dragStart = world;
                    _dragApplied = Point2D.Zero;
                }
                else
                {
                    _rubberStart = world;
                    _rubberAdditive = ctrl;
                }
                CaptureMouse();
                break;

            case EditorTool.Line:
            case EditorTool.Rectangle:
                if (_shapeStart is not { } start)
                {
                    _shapeStart = snapped;
                }
                else
                {
                    if (start != snapped)
                    {
                        session.AddObject(ActiveTool == EditorTool.Line
                            ? new LineObject { X1 = start.X, Y1 = start.Y, X2 = snapped.X, Y2 = snapped.Y }
                            : CreateRectangle(start, snapped));
                    }
                    _shapeStart = null;
                }
                RedrawOverlay();
                break;

            case EditorTool.Text:
                session.AddObject(new TextObject { X = snapped.X, Y = snapped.Y });
                ActiveTool = EditorTool.Select; // metin özellik panelinden düzenlenir
                break;

            case EditorTool.Junction:
                session.AddObject(new JunctionObject { X = snapped.X, Y = snapped.Y });
                break;

            case EditorTool.Symbol when PendingSymbol is not null:
                session.PlaceSymbol(PendingSymbol, snapped);
                break;
        }
    }

    private static RectangleObject CreateRectangle(Point2D a, Point2D b)
    {
        var r = Rect2D.FromPoints(a, b);
        return new RectangleObject { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var screen = e.GetPosition(this);
        _cursorWorld = View.ScreenToWorld(WpfConvert.ToPoint2D(screen));
        CursorMoved?.Invoke(this, _cursorWorld);

        if (_panning)
        {
            View.Pan(screen.X - _lastPanPoint.X, screen.Y - _lastPanPoint.Y);
            _lastPanPoint = screen;
            return;
        }

        if (_dragObjects is not null && Session is not null)
        {
            var total = _cursorWorld - _dragStart;
            if (Session.Settings.SnapEnabled)
            {
                total = CoordinateTransform.Snap(total, Session.Settings.GridSpacing);
            }
            var step = total - _dragApplied;
            if (step != Point2D.Zero)
            {
                // Canlı önizleme: yalnızca aktif sayfanın kabloları güncellenir.
                Session.PreviewMove(_dragObjects, step.X, step.Y);
                _dragApplied = total;
            }
            return;
        }

        if (ActiveTool == EditorTool.Symbol)
        {
            RedrawGhost();
        }
        RedrawOverlay();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _panning)
        {
            _panning = false;
            Cursor = ActiveTool == EditorTool.Select ? Cursors.Arrow : Cursors.Cross;
            ReleaseMouseCapture();
            return;
        }
        if (e.ChangedButton != MouseButton.Left || Session is null)
        {
            return;
        }

        if (_dragObjects is not null)
        {
            Session.CommitMove(_dragObjects, _dragApplied.X, _dragApplied.Y);
            _dragObjects = null;
        }
        else if (_rubberStart is { } start)
        {
            var screenRect = ScreenRect(Rect2D.FromPoints(start, _cursorWorld), 0);
            if (screenRect.Width < MinRubberBandPixels && screenRect.Height < MinRubberBandPixels)
            {
                if (!_rubberAdditive)
                {
                    Session.ClearSelection();
                }
            }
            else
            {
                Session.SelectMany(Session.ObjectsInside(Rect2D.FromPoints(start, _cursorWorld)), _rubberAdditive);
            }
            _rubberStart = null;
            RedrawOverlay();
        }
        ReleaseMouseCapture();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        RedrawGhost();
        RedrawOverlay();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelOperation(resetTool: true);
            e.Handled = true;
        }
    }

    /// <summary>Yarım kalan işlemi iptal eder; istenirse seçim aracına döner.</summary>
    public void CancelOperation(bool resetTool)
    {
        if (_dragObjects is not null && Session is not null && _dragApplied != Point2D.Zero)
        {
            Session.PreviewMove(_dragObjects, -_dragApplied.X, -_dragApplied.Y);
        }
        _dragObjects = null;
        _rubberStart = null;
        _shapeStart = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        if (resetTool)
        {
            ActiveTool = EditorTool.Select;
        }
        RedrawGhost();
        RedrawOverlay();
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
