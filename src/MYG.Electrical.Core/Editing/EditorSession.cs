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
    private readonly Dictionary<Page, IReadOnlyList<WireConnection>> _pageWires = [];

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

    /// <summary>Proje geneli bağlantı ağı (potansiyeller, netler, sayfa bağlantıları). Her değişiklikte güncellenir.</summary>
    public Network.ProjectNetwork Network { get; private set; } = null!;

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
            _selectedWire = null;
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

    /// <summary>Yeni sayfa ekler (geri alınabilir) ve ona geçer. Kapak sayfası başa eklenir.</summary>
    public Page AddPage(PageType type, string? name = null)
    {
        var page = Page.Create(name ?? (type == PageType.Cover ? "Kapak" : $"Sayfa {Document.Pages.Count + 1}"), type,
            _activePage.Format, _activePage.Orientation);
        var index = type == PageType.Cover ? 0 : Document.Pages.IndexOf(_activePage) + 1;
        ExecutePageCommand(new AddPageCommand(Document, page, index));
        ActivePage = page;
        return page;
    }

    /// <summary>Sayfayı siler (geri alınabilir). Projede en az bir sayfa kalır.</summary>
    public bool RemovePage(Page page)
    {
        if (Document.Pages.Count <= 1 || !Document.Pages.Contains(page))
        {
            return false;
        }
        var index = Document.Pages.IndexOf(page);
        ExecutePageCommand(new RemovePageCommand(Document, page));
        if (ReferenceEquals(_activePage, page))
        {
            ActivePage = Document.Pages[Math.Min(index, Document.Pages.Count - 1)];
        }
        return true;
    }

    /// <summary>Sayfanın sırasını değiştirir (geri alınabilir).</summary>
    public void MovePage(Page page, int delta)
    {
        var index = Document.Pages.IndexOf(page);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Document.Pages.Count)
        {
            return;
        }
        ExecutePageCommand(new MovePageCommand(Document, page, index, target));
    }

    private void ExecutePageCommand(IEditCommand command)
    {
        History.Execute(command);
        IsDirty = true;
        PagesChanged?.Invoke(this, EventArgs.Empty);
        SceneChanged?.Invoke(this, SceneChangedEventArgs.All); // sayfa numaraları antette değişir
    }

    // ---------- Seçim ----------

    public IReadOnlyList<DrawingObject> SelectedObjects =>
        _activePage.Objects.Where(o => _selection.Contains(o.Id)).ToList();

    public bool IsSelected(Guid id) => _selection.Contains(id);

    public void Select(DrawingObject? obj, bool additive = false)
    {
        _selectedWire = null;
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
        _selectedWire = null;
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
        if (_selection.Count == 0 && _selectedWire is null)
        {
            return;
        }
        _selection.Clear();
        _selectedWire = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private (PinRef A, PinRef B)? _selectedWire;

    /// <summary>Seçili kablo (nesne seçimiyle birlikte olmaz). Kablolar yeniden hesaplansa da uç pinlerle bulunur.</summary>
    public WireConnection? SelectedWire => _selectedWire is { } k
        ? _wires.FirstOrDefault(w => (w.From == k.A && w.To == k.B) || (w.From == k.B && w.To == k.A))
        : null;

    public void SelectWire(WireConnection? wire)
    {
        _selection.Clear();
        _selectedWire = wire is null ? null : (wire.From, wire.To);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Noktaya en yakın kablo (tolerans içinde).</summary>
    public WireConnection? HitTestWire(Point2D world, double toleranceMm) =>
        _wires.Select(w => (Wire: w, Distance: CoordinateTransform.DistanceToSegment(world, w.Start, w.End)))
            .Where(x => x.Distance <= toleranceMm)
            .OrderBy(x => x.Distance)
            .Select(x => x.Wire)
            .FirstOrDefault();

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

    public void Undo() => ApplyHistoryStep(History.PeekUndo, History.Undo);

    public void Redo() => ApplyHistoryStep(History.PeekRedo, History.Redo);

    /// <summary>
    /// Geri/ileri alma adımı: değişiklik başka bir sayfadaysa o sayfaya geçilir (kullanıcı neyin değiştiğini görür);
    /// aktif sayfa silindiyse en yakın sayfaya geçilir. Ardından seçim, sahne ve kablolar yenilenir.
    /// </summary>
    private void ApplyHistoryStep(IEditCommand? command, Action step)
    {
        if (command is null)
        {
            return;
        }
        var activeIndex = Document.Pages.IndexOf(_activePage);
        step();

        if (command is IPageListCommand)
        {
            PagesChanged?.Invoke(this, EventArgs.Empty);
        }
        var target = (command as IPageScopedCommand)?.Page;
        if (target is not null && Document.Pages.Contains(target))
        {
            ActivePage = target;
        }
        else if (!Document.Pages.Contains(_activePage))
        {
            ActivePage = Document.Pages[Math.Clamp(activeIndex, 0, Document.Pages.Count - 1)];
        }

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
        var reference = ReferenceGenerator.Next(symbol, Document, ActivePageNumber);
        var device = symbol.CreateDevice(position.X, position.Y, reference);
        device.Rotation = rotation;
        AddPlacements([device]);
        Select(device);
        return device;
    }

    /// <summary>
    /// Cihaz özellikleri penceresinin "Tamam"ı: proje cihaz kaydı, yerleşimin işlev metni ve pin adları tek geri alma
    /// adımında güncellenir. Kayıt yoksa oluşturulur.
    /// </summary>
    public void UpdateDevice(DeviceObject placement, Devices.DeviceRecord record, string description, Dictionary<string, string> designations)
    {
        var page = Document.Pages.FirstOrDefault(p => p.Objects.Contains(placement)) ?? _activePage;
        var commands = new List<IEditCommand>();
        var current = Devices.DeviceRegistry.Find(Document, record.Reference);
        if (current is null)
        {
            commands.Add(new AddDeviceRecordsCommand(Document, [record]));
        }
        else if (!ReferenceEquals(current, record))
        {
            commands.Add(new ReplaceDeviceRecordCommand(Document, current, record));
        }
        if (!string.Equals(placement.Description, description, StringComparison.Ordinal))
        {
            commands.Add(new SetPropertyCommand<string>("İşlev metni", v => placement.Description = v, placement.Description, description, placement, page));
        }
        var cleaned = designations.Where(kv => !string.IsNullOrWhiteSpace(kv.Value) && kv.Value != kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());
        if (!Devices.ContactNumbering.SameDesignations(cleaned, placement.PinDesignations))
        {
            commands.Add(new SetPropertyCommand<Dictionary<string, string>>("Pin adları", v => placement.PinDesignations = v,
                placement.PinDesignations, cleaned, placement, page));
        }
        if (commands.Count > 0)
        {
            Execute(new PageCompositeCommand("Cihaz özellikleri", page, commands), [placement.Id]);
        }
    }

    /// <summary>Cihaz kütüphanesinden yerleştirme: ana sembol + şablon bilgileriyle dolu proje cihazı (tek adım).</summary>
    public DeviceObject? PlaceDevice(Devices.DeviceTemplate template, Point2D position, double rotation = 0)
    {
        if (Catalog.Find(template.Symbol) is not { } symbol)
        {
            return null;
        }
        var reference = ReferenceGenerator.Next(symbol, Document, ActivePageNumber);
        var device = symbol.CreateDevice(position.X, position.Y, reference);
        device.Rotation = rotation;
        var records = string.IsNullOrEmpty(reference) ? [] : new[] { template.CreateRecord(reference, symbol) };
        AddPlacements([device], records);
        Select(device);
        return device;
    }

    /// <summary>
    /// Bütün cihazları IEC 81346 şablonuna göre yeniden numaralandırır: her sınıf harfi için ilk yerleşimin
    /// sayfa, kolon, yukarıdan aşağı sırasıyla 1'den başlar. Çok parçalı cihazın bütün parçaları birlikte değişir.
    /// Etiketi elle şablon dışı verilmiş cihazlar da sıraya girer. Değişen cihaz sayısını döndürür (tek geri alma adımı).
    /// </summary>
    public int RenumberDevices()
    {
        var placements = Document.Pages.SelectMany((p, pi) => p.Objects.OfType<DeviceObject>()
                .Where(d => !string.IsNullOrWhiteSpace(d.Reference))
                .Select(d => (Page: p, PageIndex: pi, Device: d, Column: Rendering.PageLayout.ColumnAt(p, d.X))))
            .ToList();
        var order = placements
            .GroupBy(x => x.Device.Reference, Devices.DeviceRegistry.ReferenceComparer)
            .Select(g => (Reference: g.Key, First: g.OrderBy(x => x.PageIndex).ThenBy(x => x.Column).ThenBy(x => x.Device.Y).ThenBy(x => x.Device.X).First(),
                Prefix: g.Select(x => Catalog.Find(x.Device.SymbolId)).FirstOrDefault(sd => sd is { Role: SymbolRole.Main or SymbolRole.Standalone })?.ReferencePrefix
                        ?? Catalog.Find(g.First().Device.SymbolId)?.ReferencePrefix ?? string.Empty))
            .Where(x => x.Prefix.Length > 0)
            .ToList();

        var mapping = new Dictionary<string, string>(Devices.DeviceRegistry.ReferenceComparer);
        foreach (var group in order.GroupBy(x => x.Prefix, StringComparer.OrdinalIgnoreCase))
        {
            var n = 0;
            foreach (var item in group.OrderBy(x => x.First.PageIndex).ThenBy(x => x.First.Column).ThenBy(x => x.First.Device.Y).ThenBy(x => x.First.Device.X))
            {
                n++;
                var format = Document.Settings.ReferenceFormat;
                var newRef = (string.IsNullOrWhiteSpace(format) ? ProjectSettings.DefaultReferenceFormat : format)
                    .Replace("{prefix}", group.Key, StringComparison.Ordinal)
                    .Replace("{page}", (item.First.PageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{n}", n.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
                if (!string.Equals(newRef, item.Reference, StringComparison.Ordinal))
                {
                    mapping[item.Reference] = newRef;
                }
            }
        }
        if (mapping.Count == 0)
        {
            return 0;
        }

        var commands = new List<IEditCommand>();
        foreach (var (page, _, device, _) in placements)
        {
            if (mapping.TryGetValue(device.Reference, out var nr))
            {
                commands.Add(new SetPropertyCommand<string>("Etiket", v => device.Reference = v, device.Reference, nr, device, page));
            }
        }
        // Kayıtlar: yerleşimi olanlar yeni etikete taşınır; yerleşimsiz bir kayıt yeni etiketle çakışırsa ayrı bir ad alır.
        var newNames = new HashSet<string>(mapping.Values, Devices.DeviceRegistry.ReferenceComparer);
        foreach (var rec in Document.Devices)
        {
            if (mapping.TryGetValue(rec.Reference, out var nr))
            {
                commands.Add(new SetPropertyCommand<string>("Cihaz etiketi", v => rec.Reference = v, rec.Reference, nr));
            }
            else if (newNames.Contains(rec.Reference))
            {
                var spare = rec.Reference + "?";
                while (Document.Devices.Any(d => Devices.DeviceRegistry.ReferenceComparer.Equals(d.Reference, spare)) || newNames.Contains(spare))
                {
                    spare += "?";
                }
                commands.Add(new SetPropertyCommand<string>("Cihaz etiketi", v => rec.Reference = v, rec.Reference, spare));
            }
        }
        Execute(new CompositeCommand("Cihazları yeniden numaralandır", commands), null);
        return mapping.Count;
    }

    /// <summary>
    /// Nesneleri ekler; yeni etiketli cihaz yerleşimleri için proje cihaz kaydını da aynı geri alma adımında oluşturur.
    /// </summary>
    public void AddPlacements(IReadOnlyList<DrawingObject> objects, IReadOnlyList<Devices.DeviceRecord>? newRecords = null)
    {
        var add = new AddObjectsCommand(_activePage, objects);
        var records = new List<Devices.DeviceRecord>(newRecords ?? []);
        foreach (var d in objects.OfType<DeviceObject>())
        {
            // Aynı cihaza eklenen yardımcı kontak bir sonraki sıra numarasını alır (13-14 → 23-24).
            // Nesne henüz sayfada olmadığı için doğrudan atanır; geri alma nesneyi tümden kaldırır.
            d.PinDesignations = Devices.ContactNumbering.DesignationsFor(Document, d, d.Reference);
            if (!string.IsNullOrWhiteSpace(d.Reference) && Devices.DeviceRegistry.Find(Document, d.Reference) is null
                && !records.Any(r => Devices.DeviceRegistry.ReferenceComparer.Equals(r.Reference, d.Reference)))
            {
                records.Add(Devices.DeviceRegistry.CreateRecord(d.Reference, Catalog.Find(d.SymbolId)));
            }
        }
        IEditCommand command = records.Count == 0
            ? add
            : new PageCompositeCommand(add.Description, _activePage, [add, new AddDeviceRecordsCommand(Document, records)]);
        Execute(command, objects.Select(o => o.Id).ToList());
    }

    /// <summary>Etkin sayfanın 1 tabanlı sırası.</summary>
    public int ActivePageNumber => Math.Max(1, Document.Pages.IndexOf(_activePage) + 1);

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
            Execute(new RotateObjectsCommand(targets, angle, _activePage), targets.Select(t => t.Id).ToList());
        }
    }

    public void MoveSelection(double dx, double dy)
    {
        var targets = SelectedObjects;
        if (targets.Count > 0 && (dx != 0 || dy != 0))
        {
            Execute(new MoveObjectsCommand(targets, dx, dy, _activePage), targets.Select(t => t.Id).ToList());
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
        History.Push(new MoveObjectsCommand(objects, totalDx, totalDy, _activePage));
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
                d.Reference = ReferenceGenerator.Next(def, Document, ActivePageNumber);
                _activePage.Objects.Add(d); // geçici: sonraki tanım numarası doğru artsın
            }
        }
        foreach (var d in copies.OfType<DeviceObject>())
        {
            _activePage.Objects.Remove(d);
        }
        AddPlacements(copies);
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
        Execute(new SetPropertyCommand<T>(description, setter, oldValue, newValue, target, _activePage), [target.Id]);
    }

    /// <summary>Kullanıcının hiçbir bilgi girmediği (sembolden otomatik oluşan) cihaz kaydı mı.</summary>
    private static bool IsUntouched(Devices.DeviceRecord r) =>
        !r.HasCommercialData && r.Description.Length == 0 && r.Location.Length == 0 && r.Notes.Length == 0
        && r.Datasheet.Length == 0 && r.TemplateId.Length == 0;

    /// <summary>Otomatik kablo numaralandırma (tek geri alma adımı). Değişen kablo sayısını döndürür.</summary>
    public int NumberWires(bool renumberAll)
    {
        var plan = Core.Network.WireNumbering.Plan(Document, Network, renumberAll);
        var command = new SetWireDataCommand("Kablo numaralandırma", plan);
        if (command.IsEmpty)
        {
            return 0;
        }
        var changed = plan.Sum(kv => kv.Value.Count(n => kv.Key.FindWireData(n.A, n.B) is not { } o || o.Number != n.Number));
        Execute(command, null);
        return changed;
    }

    /// <summary>Etkin sayfadaki bir kablonun bilgilerini değiştirir (geri alınabilir).</summary>
    public void SetWireData(WireConnection wire, Devices.WireData data)
    {
        var list = _activePage.WireData.Select(w => w.Clone()).ToList();
        list.RemoveAll(w => w.Connects(wire.From, wire.To));
        var copy = data.Clone();
        copy.A = wire.From;
        copy.B = wire.To;
        if (!copy.IsEmpty || copy.NumberLocked)
        {
            list.Add(copy);
        }
        Execute(new SetWireDataCommand("Kablo bilgisi", new Dictionary<Page, List<Devices.WireData>> { [_activePage] = list }), null);
    }

    /// <summary>
    /// Yerleşimin etiketini değiştirir ve proje cihaz kaydını tutarlı tutar (tek geri alma adımı):
    /// yeni etiketin kaydı yoksa, eski kayıt başka yerleşimce kullanılmıyorsa yeniden adlandırılır,
    /// kullanılıyorsa sembolden yeni kayıt oluşturulur. Var olan bir etikete geçmek cihazı o cihazın parçası yapar.
    /// </summary>
    public void RenameReference(DeviceObject placement, string newReference)
    {
        newReference = newReference.Trim();
        var oldReference = placement.Reference;
        if (string.Equals(oldReference, newReference, StringComparison.Ordinal))
        {
            return;
        }
        var commands = new List<IEditCommand>
        {
            new SetPropertyCommand<string>("Tanım", v => placement.Reference = v, oldReference, newReference, placement, _activePage),
        };
        var designations = Devices.ContactNumbering.DesignationsFor(Document, placement, newReference);
        if (!Devices.ContactNumbering.SameDesignations(designations, placement.PinDesignations))
        {
            commands.Add(new SetPropertyCommand<Dictionary<string, string>>("Kontak numarası", v => placement.PinDesignations = v,
                placement.PinDesignations, designations, placement, _activePage));
        }
        var oldRecordForCleanup = Devices.DeviceRegistry.Find(Document, oldReference);
        var oldUsedElsewhere = Devices.DeviceRegistry.Placements(Document, oldReference).Any(x => !ReferenceEquals(x.Placement, placement));
        if (newReference.Length > 0 && Devices.DeviceRegistry.Find(Document, newReference) is not null
            && oldRecordForCleanup is not null && !oldUsedElsewhere && IsUntouched(oldRecordForCleanup))
        {
            // Parça başka bir cihaza katıldı: kendi başına oluşturulmuş, bilgi girilmemiş kaydı artık gereksiz.
            commands.Add(new RemoveDeviceRecordsCommand(Document, [oldRecordForCleanup]));
        }
        if (newReference.Length > 0 && Devices.DeviceRegistry.Find(Document, newReference) is null)
        {
            var oldRecord = Devices.DeviceRegistry.Find(Document, oldReference);
            var othersUseOld = Devices.DeviceRegistry.Placements(Document, oldReference).Any(x => !ReferenceEquals(x.Placement, placement));
            if (oldRecord is not null && !othersUseOld)
            {
                commands.Add(new SetPropertyCommand<string>("Cihaz etiketi", v => oldRecord.Reference = v, oldRecord.Reference, newReference));
            }
            else
            {
                var record = oldRecord?.Clone() ?? Devices.DeviceRegistry.CreateRecord(newReference, Catalog.Find(placement.SymbolId));
                record.Reference = newReference;
                commands.Add(new AddDeviceRecordsCommand(Document, [record]));
            }
        }
        Execute(new PageCompositeCommand("Tanım", _activePage, commands), [placement.Id]);
    }

    /// <summary>Proje/sayfa bilgisi gibi nesne dışı bir değeri geri alınabilir şekilde değiştirir (tam yeniden çizim).</summary>
    /// <param name="page">Değişiklik bir sayfaya aitse (ad, format...), geri alınırken o sayfaya geçilir.</param>
    public void SetDocumentProperty<T>(string description, Func<T> getter, Action<T> setter, T newValue, Page? page = null)
    {
        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }
        Execute(new SetPropertyCommand<T>(description, setter, oldValue, newValue, page: page), null);
        if (page is not null)
        {
            PagesChanged?.Invoke(this, EventArgs.Empty); // sayfa listesindeki ad/format güncellensin
        }
    }

    /// <summary>Sayfa çerçevesi/antet gibi sayfa genelini etkileyen değişikliklerden sonra çağrılır.</summary>
    public void NotifyPageChanged()
    {
        IsDirty = true;
        SceneChanged?.Invoke(this, SceneChangedEventArgs.All);
    }

    /// <summary>
    /// Etkin sayfanın kablolarını yeniden hesaplar, diğer sayfaların önbelleğini kullanarak proje ağını kurar.
    /// Düzenlemeler yalnızca etkin sayfada yapıldığından diğer sayfaların kabloları değişmez.
    /// </summary>
    public void RecomputeWires()
    {
        _wires = _wireEngine.Compute(_activePage.Objects);
        _pageWires[_activePage] = _wires;
        foreach (var stale in _pageWires.Keys.Where(p => !Document.Pages.Contains(p)).ToList())
        {
            _pageWires.Remove(stale);
        }
        foreach (var page in Document.Pages.Where(p => !_pageWires.ContainsKey(p)))
        {
            _pageWires[page] = _wireEngine.Compute(page.Objects);
        }
        Network = Core.Network.ProjectNetwork.Build(Document, Catalog, _pageWires);
        WiresChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Tüm sayfaların kablolarını baştan hesaplar (dosya açılışı, toplu işlemler).</summary>
    public void RecomputeAllWires()
    {
        _pageWires.Clear();
        RecomputeWires();
    }

    private void AfterChange(IReadOnlyCollection<Guid>? affected, bool markDirty = true)
    {
        if (markDirty)
        {
            IsDirty = true;
        }
        var wiringAffected = affected is null
            || affected.Any(id => _activePage.Find(id) is not { } o || o.AffectsWiring);
        // Önce ağ: nesneler (sayfa bağlantısı referansı, potansiyel rengi) güncel ağla çizilsin.
        if (wiringAffected)
        {
            RecomputeWires();
        }
        SceneChanged?.Invoke(this, affected is null ? SceneChangedEventArgs.All : new SceneChangedEventArgs(affected));
    }
}
