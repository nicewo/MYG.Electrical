using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MYG.Electrical.Core.AutoWire;
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

    private static readonly Brush PageBrush = Frozen(new SolidColorBrush(System.Windows.Media.Colors.White));
    private static readonly Brush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0)));
    private static readonly Pen GridPenBase = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xE3, 0xE5, 0xE8))), 1));
    private static readonly Brush SelectionBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)));
    private static readonly Brush ActiveToolBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xF2, 0x8C, 0x28)));
    private static readonly Brush RubberFill = Frozen(new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0x98, 0x00)));
    private static readonly Pen SelectionPen = Frozen(new Pen(SelectionBrush, 1.5));
    private static readonly Brush SelectionFill = Frozen(new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0x98, 0x00)));
    private static readonly Pen WireSelectionPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0x98, 0x00))), 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Pen ErrorPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F))), 2) { DashStyle = DashStyles.Dash });
    private static readonly Brush ErrorFill = Frozen(new SolidColorBrush(Color.FromArgb(0x26, 0xD3, 0x2F, 0x2F)));
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
    private Point2D _dragSnapOrigin;
    private bool _suppressContextMenu;
    private Point2D _dragApplied;
    private Point2D? _rubberStart;
    private bool _rubberAdditive;
    private Point2D? _shapeStart;
    private Point2D _cursorWorld;

    // Yerleştirme önizlemesi: kullanıcı R ile döndürene kadar junction yönü otomatik önerilir.
    private double _placementRotation;
    private bool _manualRotation;

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

    /// <summary>Pin aracıyla tıklandı: konum ve yönü belli pin; ad ve ekleme ana pencerede yapılır.</summary>
    public event EventHandler<DevicePinObject>? DevicePinRequested;

    public static readonly DependencyProperty PendingPinProperty = DependencyProperty.Register(
        nameof(PendingPin), typeof(DevicePinObject), typeof(DrawingCanvas),
        new PropertyMetadata(null, (d, _) => ((DrawingCanvas)d).RedrawGhost()));

    /// <summary>Pin aracının şablonu (sıradaki ad, işlev, başlangıç yönü).</summary>
    public DevicePinObject? PendingPin
    {
        get => (DevicePinObject?)GetValue(PendingPinProperty);
        set => SetValue(PendingPinProperty, value);
    }

    /// <summary>Seç aracıyla bir nesneye çift tıklandı.</summary>
    public event EventHandler<DrawingObject>? ObjectDoubleClicked;

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

    public static readonly DependencyProperty PendingImageProperty = DependencyProperty.Register(
        nameof(PendingImage), typeof(ImageObject), typeof(DrawingCanvas),
        new PropertyMetadata(null, (d, _) => ((DrawingCanvas)d).RedrawGhost()));

    /// <summary>Resim aracı için hazırlanmış (kaynağı projeye eklenmiş) resim şablonu.</summary>
    public ImageObject? PendingImage
    {
        get => (ImageObject?)GetValue(PendingImageProperty);
        set => SetValue(PendingImageProperty, value);
    }

    public static readonly DependencyProperty PendingConnectorProperty = DependencyProperty.Register(
        nameof(PendingConnector), typeof(SinglePinObject), typeof(DrawingCanvas),
        new PropertyMetadata(null, (d, _) => ((DrawingCanvas)d).RedrawGhost()));

    /// <summary>Potansiyel / sayfa bağlantısı aracı için şablon (ad, tür). Her tıklamada kopyası yerleştirilir.</summary>
    public SinglePinObject? PendingConnector
    {
        get => (SinglePinObject?)GetValue(PendingConnectorProperty);
        set => SetValue(PendingConnectorProperty, value);
    }

    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors), typeof(ColorScheme), typeof(DrawingCanvas),
        new PropertyMetadata(ColorScheme.Default, (d, _) => ((DrawingCanvas)d).RefreshAll()));

    /// <summary>Şema renkleri (kullanıcı ayarı).</summary>
    public ColorScheme Colors
    {
        get => (ColorScheme)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    /// <summary>DRC bulgularında vurgulanacak nesneler (kırmızı çerçeve).</summary>
    public static readonly DependencyProperty HighlightedObjectsProperty = DependencyProperty.Register(
        nameof(HighlightedObjects), typeof(IReadOnlyCollection<Guid>), typeof(DrawingCanvas),
        new PropertyMetadata(null, (d, _) => ((DrawingCanvas)d).RedrawOverlay()));

    public IReadOnlyCollection<Guid>? HighlightedObjects
    {
        get => (IReadOnlyCollection<Guid>?)GetValue(HighlightedObjectsProperty);
        set => SetValue(HighlightedObjectsProperty, value);
    }

    public static readonly DependencyProperty PendingDeviceProperty = DependencyProperty.Register(
        nameof(PendingDevice), typeof(Core.Devices.DeviceTemplate), typeof(DrawingCanvas), new PropertyMetadata(null));

    /// <summary>Sembol aracı bir cihaz kütüphanesi kaydıyla başlatıldıysa: yerleştirilen cihaz bu bilgilerle oluşur.</summary>
    public Core.Devices.DeviceTemplate? PendingDevice
    {
        get => (Core.Devices.DeviceTemplate?)GetValue(PendingDeviceProperty);
        set => SetValue(PendingDeviceProperty, value);
    }

    /// <summary>Ekran noktasının (bu denetime göre) dünya koordinatı; sürükle-bırak için.</summary>
    public Point2D ScreenToWorld(Point screen) => View.ScreenToWorld(WpfConvert.ToPoint2D(screen));

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

    /// <summary>Yerleştirilecek sembol/junction önizlemesini 90° döndürür (R tuşu).</summary>
    public void RotatePlacement()
    {
        _placementRotation = CoordinateTransform.NormalizeRotation(EffectivePlacementRotation() + GeometryConstants.RightAngle);
        _manualRotation = true;
        RedrawGhost();
    }

    /// <summary>Junction araçlarında, elle döndürülmediyse çevredeki pinlere göre önerilen yön.</summary>
    private double EffectivePlacementRotation()
    {
        if (_manualRotation || Session is null || ActiveTool.ToJunctionShape() is not { } shape)
        {
            return _placementRotation;
        }
        return JunctionPlacement.SuggestRotation(Session.ActivePage.Objects, Session.Snap(_cursorWorld), shape, _placementRotation);
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
        WpfSceneSink.ClearResourceCache();
        _composer = newSession is null ? null : new SceneComposer(newSession.Catalog, newSession.Document.FindResource);
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

    private void OnWiresChanged(object? sender, EventArgs e)
    {
        RedrawWires();
        RedrawOverlay(); // seçili kablo yeniden hesaplanan konumuyla vurgulansın
    }

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
        _placementRotation = 0;
        _manualRotation = false;
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
        if (_composer is not null && Session is not null)
        {
            _composer.Network = Session.Network;
            _composer.Colors = Colors;
            _composer.ComposeObject(obj, new WpfSceneSink(dc), EditorOptions);
        }
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
        PageFrameComposer.Compose(doc.Project, page, doc.Pages.IndexOf(page) + 1, doc.Pages.Count, new WpfSceneSink(dc), Colors);
    }

    private void RedrawWires()
    {
        using var dc = _wireVisual.RenderOpen();
        if (Session is not null)
        {
            if (_composer is not null)
            {
                _composer.Network = Session.Network;
                _composer.Colors = Colors;
                _composer.ComposeWires(Session.ActivePage, Session.Wires, new WpfSceneSink(dc));
            }
        }
    }

    private void RedrawGhost()
    {
        using var dc = _ghostVisual.RenderOpen();
        if (Session is null || _composer is null || !IsMouseOver || !ActiveTool.IsPlacementTool())
        {
            return;
        }
        var p = Session.Snap(_cursorWorld);
        DrawingObject? ghost = ActiveTool switch
        {
            _ when ActiveTool.ToJunctionShape() is { } shape =>
                new JunctionObject { X = p.X, Y = p.Y, Shape = shape, Rotation = EffectivePlacementRotation() },
            EditorTool.Image => PendingImageAt(p),
            EditorTool.Potential or EditorTool.PageLink => PendingConnectorAt(p),
            EditorTool.DevicePin => PendingPinAt(p),
            _ => PendingSymbol?.CreateDevice(p.X, p.Y, string.Empty),
        };
        if (ghost is DeviceObject d)
        {
            d.Rotation = _placementRotation;
        }
        if (ghost is not null)
        {
            _composer.ComposeObject(ghost, new WpfSceneSink(dc), SceneComposerOptions.Editor);
        }
    }

    /// <summary>Seçim çerçeveleri, seçim dikdörtgeni ve araç önizlemeleri (ekran koordinatlarında).</summary>
    private void RedrawOverlay()
    {
        using var dc = _overlayVisual.RenderOpen();
        if (Session is null)
        {
            return;
        }

        if (HighlightedObjects is { Count: > 0 } highlighted)
        {
            foreach (var obj in Session.ActivePage.Objects.Where(o => highlighted.Contains(o.Id)))
            {
                dc.DrawRectangle(ErrorFill, ErrorPen, ScreenRect(ObjectBounds.Get(obj, Session.Catalog), SelectionPaddingPixels + 2));
            }
        }

        foreach (var obj in Session.SelectedObjects)
        {
            var b = ObjectBounds.Get(obj, Session.Catalog);
            dc.DrawRectangle(SelectionFill, SelectionPen, ScreenRect(b, SelectionPaddingPixels));
        }

        if (Session.SelectedWire is { } wire)
        {
            dc.DrawLine(WireSelectionPen, ToScreen(wire.Start), ToScreen(wire.End));
        }

        if (_rubberStart is { } rs)
        {
            dc.DrawRectangle(RubberFill, RubberPen, ScreenRect(Rect2D.FromPoints(rs, _cursorWorld), 0));
        }

        if ((ActiveTool is EditorTool.Line or EditorTool.Rectangle or EditorTool.Text || ActiveTool.IsPlacementTool()) && IsMouseOver)
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
                else if (ActiveTool is EditorTool.Rectangle or EditorTool.DeviceBox)
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
                if (ActiveTool == EditorTool.Select && _dragObjects is null && _rubberStart is null)
                {
                    // Sağ tık menüsü: tıklanan nesne seçili değilse onu seç (menü komutları ona uygulanır)
                    var hit = Session.HitTest(world, View.ScreenLengthToWorld(GeometryConstants.HitTolerancePixels));
                    if (hit is not null && !Session.IsSelected(hit.Id))
                    {
                        Session.Select(hit);
                    }
                    _suppressContextMenu = false;
                }
                else
                {
                    // Bir araç veya işlem sürerken sağ tık = iptal (menü açılmaz)
                    CancelOperation(resetTool: true);
                    _suppressContextMenu = true;
                }
                break;
            case MouseButton.Left when e.ClickCount == 2 && ActiveTool == EditorTool.Select
                && Session.HitTest(world, View.ScreenLengthToWorld(GeometryConstants.HitTolerancePixels)) is { } target:
                // Çift tık: sürükleme başlatılmaz, nesnenin ayrıntı penceresi açılır (cihaz özellikleri)
                _dragObjects = null;
                if (IsMouseCaptured)
                {
                    ReleaseMouseCapture();
                }
                ObjectDoubleClicked?.Invoke(this, target);
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
                    _dragSnapOrigin = hit.SnapPoint;
                    _dragApplied = Point2D.Zero;
                }
                else if (!ctrl && session.HitTestWire(world, View.ScreenLengthToWorld(GeometryConstants.HitTolerancePixels)) is { } wire)
                {
                    session.SelectWire(wire); // kablo bilgisi sağ panelde düzenlenir
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
            case EditorTool.DeviceBox:
                if (_shapeStart is not { } start)
                {
                    _shapeStart = snapped;
                }
                else
                {
                    if (start != snapped)
                    {
                        DrawingObject shape = ActiveTool switch
                        {
                            EditorTool.Line => new LineObject { X1 = start.X, Y1 = start.Y, X2 = snapped.X, Y2 = snapped.Y },
                            EditorTool.DeviceBox => WithDash(CreateRectangle(start, snapped)),
                            _ => CreateRectangle(start, snapped),
                        };
                        session.AddObject(shape);
                    }
                    _shapeStart = null;
                }
                RedrawOverlay();
                break;

            case EditorTool.Text:
                session.AddObject(new TextObject { X = snapped.X, Y = snapped.Y });
                ActiveTool = EditorTool.Select; // metin özellik panelinden düzenlenir
                break;

            case EditorTool.Angle or EditorTool.Tee or EditorTool.Cross:
                session.PlaceJunction(ActiveTool.ToJunctionShape()!.Value, snapped, EffectivePlacementRotation());
                break;

            case EditorTool.Image when PendingImageAt(snapped) is { } image:
                session.AddObject(image);
                ActiveTool = EditorTool.Select; // resim genelde tek tek yerleştirilir
                break;

            case EditorTool.DevicePin when PendingPinAt(snapped) is { } pin:
                // Ad sorma/numaralandırma ana pencerede yapılır (bağlantı adı penceresi, X0 → X1 ...)
                DevicePinRequested?.Invoke(this, pin);
                break;

            case EditorTool.Potential or EditorTool.PageLink when PendingConnectorAt(snapped) is { } connector:
                session.AddObject(connector);
                break;

            case EditorTool.Symbol when PendingDevice is { } template && PendingSymbol?.Id == template.Symbol:
                session.PlaceDevice(template, snapped, _placementRotation);
                break;

            case EditorTool.Symbol when PendingSymbol is not null:
                session.PlaceSymbol(PendingSymbol, snapped, _placementRotation);
                break;
        }
    }

    private DevicePinObject? PendingPinAt(Point2D p)
    {
        if (PendingPin?.CloneWithNewId() is not DevicePinObject pin)
        {
            return null;
        }
        pin.X = p.X;
        pin.Y = p.Y;
        pin.Rotation = PendingPin.Rotation + _placementRotation;
        return pin;
    }

    private SinglePinObject? PendingConnectorAt(Point2D p)
    {
        if (PendingConnector?.CloneWithNewId() is not SinglePinObject c)
        {
            return null;
        }
        c.X = p.X;
        c.Y = p.Y;
        c.Rotation = _placementRotation;
        return c;
    }

    private ImageObject? PendingImageAt(Point2D topLeft)
    {
        if (PendingImage?.CloneWithNewId() is not ImageObject image)
        {
            return null;
        }
        image.X = topLeft.X;
        image.Y = topLeft.Y;
        return image;
    }

    private static RectangleObject WithDash(RectangleObject r)
    {
        r.Dashed = true;
        return r;
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
                // Tutulan nesnenin referans noktası grid'e oturur (grid dışında kalmış nesne de hizaya gelir)
                total = CoordinateTransform.SnapDelta(_dragSnapOrigin, total, Session.Settings.GridSpacing);
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

        if (ActiveTool.IsPlacementTool())
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

    protected override void OnContextMenuOpening(System.Windows.Controls.ContextMenuEventArgs e)
    {
        if (_suppressContextMenu || ActiveTool != EditorTool.Select)
        {
            e.Handled = true;
        }
        _suppressContextMenu = false;
        base.OnContextMenuOpening(e);
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
