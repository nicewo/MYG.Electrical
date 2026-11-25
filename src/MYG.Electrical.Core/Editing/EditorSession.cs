using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Editing;

/// <summary>
/// Açık proje üzerindeki düzenleme oturumu: aktif sayfa, seçim, geri al/ileri al ve kablo önbelleği.
/// UI'dan bağımsızdır; WPF katmanı yalnızca olayları dinler.
/// </summary>
public sealed class EditorSession
{
    private readonly HashSet<Guid> _selection = [];
    private readonly AutoWireEngine _wireEngine;
    private List<DrawingObject> _clipboard = [];
    private Page _activePage;
    private IReadOnlyList<WireConnection> _wires = [];

    public EditorSession(ProjectDocument document, ISymbolCatalog catalog, AutoWireEngine? wireEngine = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Pages.Count == 0)
        {
            document.Pages.Add(Page.Create("Sayfa 1", PageType.Schematic));
        }
        Document = document;
        Catalog = catalog;
        _wireEngine = wireEngine ?? new AutoWireEngine();
        _activePage = document.Pages.FirstOrDefault(p => p.Type == PageType.Schematic) ?? document.Pages[0];
        History.Changed += (_, _) => HistoryChanged?.Invoke(this, EventArgs.Empty);
        RecomputeWires();
    }

    public ProjectDocument Document { get; }
    public ISymbolCatalog Catalog { get; }
    public UndoRedoManager History { get; } = new();
    public EditorSettings Settings { get; } = new();
    public bool IsDirty { get; set; }

    public IReadOnlyList<WireConnection> Wires => _wires;

    public Page ActivePage
    {
        get => _activePage;
        set
        {
            if (ReferenceEquals(_activePage, value))
            {
                return;
            }
            _activePage = value;
            _selection.Clear();
            RecomputeWires();
            ActivePageChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ActivePageChanged;
    public event EventHandler<SceneChangedEventArgs>? SceneChanged;
    public event EventHandler? SelectionChanged;
    public event EventHandler? WiresChanged;
    public event EventHandler? HistoryChanged;

    public event EventHandler? PagesChanged;

    // ---------- Sayfa yönetimi ----------

    public Page AddPage(PageType type, string? name = null)
    {
        var page = Page.Create(name ?? (type == PageType.Cover ? "Kapak" : $"Sayfa {Document.Pages.Count + 1}"), type,
            _activePage.Format, _activePage.Orientation);
        var index = type == PageType.Cover ? 0 : Document.Pages.IndexOf(_activePage) + 1;
        Document.Pages.Insert(Math.Clamp(index, 0, Document.Pages.Count), page);
        IsDirty = true;
        PagesChanged?.Invoke(this, EventArgs.Empty);
        ActivePage = page;
        return page;
    }

    /// <summary>Sayfayı siler. Projede en az bir sayfa kalır.</summary>
    public bool RemovePage(Page page)
    {
        if (Document.Pages.Count <= 1 || !Document.Pages.Contains(page))
        {
            return false;
        }
        var index = Document.Pages.IndexOf(page);
        Document.Pages.Remove(page);
        History.Clear(); // silinen sayfaya ait geri alma adımları geçersiz kalır
        IsDirty = true;
        PagesChanged?.Invoke(this, EventArgs.Empty);
        if (ReferenceEquals(_activePage, page))
        {
            ActivePage = Document.Pages[Math.Min(index, Document.Pages.Count - 1)];
        }
        else
        {
            SceneChanged?.Invoke(this, SceneChangedEventArgs.All); // sayfa numaraları değişti
        }
        return true;
    }

    public void MovePage(Page page, int delta)
    {
        var index = Document.Pages.IndexOf(page);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Document.Pages.Count)
        {
            return;
        }
        Document.Pages.RemoveAt(index);
        Document.Pages.Insert(target, page);
        IsDirty = true;
        PagesChanged?.Invoke(this, EventArgs.Empty);
        SceneChanged?.Invoke(this, SceneChangedEventArgs.All);
    }

    // ---------- Seçim ----------

    public IReadOnlyList<DrawingObject> SelectedObjects =>
        _activePage.Objects.Where(o => _selection.Contains(o.Id)).ToList();

    public bool IsSelected(Guid id) => _selection.Contains(id);

    public void Select(DrawingObject? obj, bool additive = false)
    {
        if (!additive)
        {
            _selection.Clear();
        }
        if (obj is not null && !_selection.Add(obj.Id) && additive)
        {
            _selection.Remove(obj.Id); // Ctrl+tık: seçimi tersine çevir
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectMany(IEnumerable<DrawingObject> objects, bool additive = false)
    {
        if (!additive)
        {
            _selection.Clear();
        }
        foreach (var o in objects)
        {
            _selection.Add(o.Id);
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll() => SelectMany(_activePage.Objects);

    public void ClearSelection()
    {
        if (_selection.Count == 0)
        {
            return;
        }
        _selection.Clear();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Sorgular ----------

    /// <summary>En üstteki (son eklenen) isabet eden nesne.</summary>
    public DrawingObject? HitTest(Point2D world, double toleranceMm)
    {
        for (var i = _activePage.Objects.Count - 1; i >= 0; i--)
        {
            var o = _activePage.Objects[i];
            if (ObjectBounds.HitTest(o, world, toleranceMm, Catalog))
            {
                return o;
            }
        }
        return null;
    }

    /// <summary>Dikdörtgen içinde tamamen kalan nesneler.</summary>
    public IEnumerable<DrawingObject> ObjectsInside(Rect2D area) =>
        _activePage.Objects.Where(o => area.Contains(ObjectBounds.Get(o, Catalog)));

    public Point2D Snap(Point2D p) => Settings.SnapEnabled ? CoordinateTransform.Snap(p, Settings.GridSpacing) : p;

    // ---------- Düzenleme ----------

    public void Execute(IEditCommand command, IReadOnlyCollection<Guid>? affected = null)
    {
        History.Execute(command);
        AfterChange(affected);
    }

    public void Undo()
    {
        History.Undo();
        _selection.RemoveWhere(id => _activePage.Find(id) is null);
        AfterChange(null);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        History.Redo();
        _selection.RemoveWhere(id => _activePage.Find(id) is null);
        AfterChange(null);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddObject(DrawingObject obj, bool select = true)
    {
        Execute(new AddObjectsCommand(_activePage, [obj]), [obj.Id]);
        if (select)
        {
            Select(obj);
        }
    }

    public DeviceObject PlaceSymbol(SymbolDefinition symbol, Point2D position, double rotation = 0)
    {
        var reference = ReferenceGenerator.Next(symbol.ReferencePrefix, Document.Pages);
        var device = symbol.CreateDevice(position.X, position.Y, reference);
        device.Rotation = rotation;
        AddObject(device);
        return device;
    }

    public JunctionObject PlaceJunction(JunctionShape shape, Point2D position, double rotation = 0)
    {
        var junction = new JunctionObject { X = position.X, Y = position.Y, Shape = shape, Rotation = rotation };
        AddObject(junction);
        return junction;
    }

    public void DeleteSelection()
    {
        var targets = SelectedObjects;
        if (targets.Count == 0)
        {
            return;
        }
        _selection.Clear();
        Execute(new RemoveObjectsCommand(_activePage, targets), targets.Select(t => t.Id).ToList());
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RotateSelection(double angle = GeometryConstants.RightAngle)
    {
        var targets = SelectedObjects;
        if (targets.Count > 0)
        {
            Execute(new RotateObjectsCommand(targets, angle), targets.Select(t => t.Id).ToList());
        }
    }

    public void MoveSelection(double dx, double dy)
    {
        var targets = SelectedObjects;
        if (targets.Count > 0 && (dx != 0 || dy != 0))
        {
            Execute(new MoveObjectsCommand(targets, dx, dy), targets.Select(t => t.Id).ToList());
        }
    }

    /// <summary>Sürükleme sırasında canlı önizleme: geçmişe yazmadan taşır, yalnızca aktif sayfanın kablolarını günceller.</summary>
    public void PreviewMove(IReadOnlyList<DrawingObject> objects, double dx, double dy)
    {
        foreach (var o in objects)
        {
            o.Move(dx, dy);
        }
        AfterChange(objects.Select(o => o.Id).ToList(), markDirty: false);
    }

    /// <summary>Sürükleme bitince toplam taşımayı tek geri alma adımı olarak kaydeder.</summary>
    public void CommitMove(IReadOnlyList<DrawingObject> objects, double totalDx, double totalDy)
    {
        if (objects.Count == 0 || (totalDx == 0 && totalDy == 0))
        {
            return;
        }
        History.Push(new MoveObjectsCommand(objects, totalDx, totalDy));
        IsDirty = true;
    }

    public void CopySelection() => _clipboard = SelectedObjects.Select(o => o.CloneWithNewId()).ToList();

    public bool CanPaste => _clipboard.Count > 0;

    public void Paste()
    {
        if (_clipboard.Count == 0)
        {
            return;
        }
        var copies = _clipboard.Select(o => o.CloneWithNewId()).ToList();
        foreach (var c in copies)
        {
            c.Move(Settings.PasteOffset, Settings.PasteOffset);
            if (c is DeviceObject d && Catalog.Find(d.SymbolId) is { } def)
            {
                d.Reference = ReferenceGenerator.Next(def.ReferencePrefix, Document.Pages);
                _activePage.Objects.Add(d); // geçici: sonraki tanım numarası doğru artsın
            }
        }
        foreach (var d in copies.OfType<DeviceObject>())
        {
            _activePage.Objects.Remove(d);
        }
        Execute(new AddObjectsCommand(_activePage, copies), copies.Select(c => c.Id).ToList());
        SelectMany(copies);
    }

    /// <summary>Bir özellik değişikliğini geri alınabilir şekilde uygular.</summary>
    public void SetProperty<T>(DrawingObject target, string description, Func<T> getter, Action<T> setter, T newValue)
    {
        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }
        Execute(new SetPropertyCommand<T>(description, setter, oldValue, newValue, target), [target.Id]);
    }

    /// <summary>Proje/sayfa bilgisi gibi nesne dışı bir değeri geri alınabilir şekilde değiştirir (tam yeniden çizim).</summary>
    public void SetDocumentProperty<T>(string description, Func<T> getter, Action<T> setter, T newValue)
    {
        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }
        Execute(new SetPropertyCommand<T>(description, setter, oldValue, newValue), null);
    }

    /// <summary>Sayfa çerçevesi/antet gibi sayfa genelini etkileyen değişikliklerden sonra çağrılır.</summary>
    public void NotifyPageChanged()
    {
        IsDirty = true;
        SceneChanged?.Invoke(this, SceneChangedEventArgs.All);
    }

    public void RecomputeWires()
    {
        _wires = _wireEngine.Compute(_activePage.Objects);
        WiresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AfterChange(IReadOnlyCollection<Guid>? affected, bool markDirty = true)
    {
        if (markDirty)
        {
            IsDirty = true;
        }
        var wiringAffected = affected is null
            || affected.Any(id => _activePage.Find(id) is not { } o || o.AffectsWiring);
        SceneChanged?.Invoke(this, affected is null ? SceneChangedEventArgs.All : new SceneChangedEventArgs(affected));
        if (wiringAffected)
        {
            RecomputeWires();
        }
    }
}
