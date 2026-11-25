using System.Collections.ObjectModel;
using System.Globalization;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.App.Services;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Sağ panel: seçili nesne, yoksa sayfa ve proje (kapak/antet) özellikleri.</summary>
public sealed class PropertiesViewModel(IDialogService dialogs, Action<ImageObject> editImage, Action fileImagesChanged) : ObservableObject
{
    private static readonly IReadOnlyList<string> Rotations = ["0", "90", "180", "270"];
    private static readonly IReadOnlyList<string> Formats = Enum.GetNames<PageFormat>();
    private static readonly Dictionary<PageOrientation, string> OrientationNames = new()
    {
        [PageOrientation.Landscape] = "Yatay",
        [PageOrientation.Portrait] = "Dikey",
    };
    private static readonly Dictionary<PageType, string> PageTypeNames = new()
    {
        [PageType.Cover] = "Kapak",
        [PageType.Schematic] = "Şema",
    };

    private EditorSession? _session;
    private string _header = string.Empty;

    public ObservableCollection<PropertyRow> Rows { get; } = [];

    public string Header
    {
        get => _header;
        private set => SetField(ref _header, value);
    }

    public void Attach(EditorSession? session)
    {
        if (_session is not null)
        {
            _session.SelectionChanged -= OnSelectionChanged;
            _session.SceneChanged -= OnSceneChanged;
            _session.ActivePageChanged -= OnSelectionChanged;
        }
        _session = session;
        if (session is not null)
        {
            session.SelectionChanged += OnSelectionChanged;
            session.SceneChanged += OnSceneChanged;
            session.ActivePageChanged += OnSelectionChanged;
        }
        Rebuild();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => Rebuild();

    private void OnSceneChanged(object? sender, SceneChangedEventArgs e)
    {
        foreach (var row in Rows)
        {
            row.Refresh();
        }
    }

    private void Rebuild()
    {
        Rows.Clear();
        if (_session is null)
        {
            Header = string.Empty;
            return;
        }
        var selected = _session.SelectedObjects;
        switch (selected.Count)
        {
            case 0 when _session.SelectedWire is not null:
                BuildWireRows();
                break;
            case 0:
                Header = "Sayfa ve Proje";
                BuildPageRows(_session.ActivePage);
                BuildProjectRows(_session.Document.Project);
                break;
            case 1:
                BuildObjectRows(selected[0]);
                break;
            default:
                Header = $"{selected.Count} nesne seçili";
                Rows.Add(new TextRow("Bilgi", () => "Taşı: sürükle · Döndür: R · Sil: Del", null));
                break;
        }
    }

    // ---------- Nesne ----------

    private void BuildObjectRows(DrawingObject obj)
    {
        var s = _session!;
        void Set<T>(string name, Func<T> get, Action<T> set, T value) => s.SetProperty(obj, name, get, set, value);
        NumberRow Num(string label, Func<double> get, Action<double> set) => new(label, get, v => Set(label, get, set, v));

        switch (obj)
        {
            case DeviceObject d:
                var def = s.Catalog.Find(d.SymbolId);
                Header = $"Cihaz {d.Reference}";
                Rows.Add(new TextRow("Tanım", () => d.Reference, v => s.RenameReference(d, v)));
                Rows.Add(new TextRow("Sembol", () => def?.Name ?? d.SymbolId, null));
                Rows.Add(new TextRow("İşlev metni", () => d.Description, v => Set("İşlev metni", () => d.Description, x => d.Description = x, v.Trim())));
                AddSymbolTransformRows(d, Num);
                AddDeviceRecordRows(d);
                Rows.Add(new SectionRow("Bağlantı noktaları"));
                foreach (var pin in d.ConnectionPoints)
                {
                    var p = pin;
                    Rows.Add(new TextRow(d.PinLabel(p.Id), () => DescribePin(d, p), null));
                }
                break;

            case PotentialObject pot:
                Header = $"Potansiyel {pot.Name}";
                Rows.Add(new TextRow("Ad", () => pot.Name, v => SetPotentialName(pot, v.Trim())));
                Rows.Add(new ChoiceRow("Tür", PotentialKindNames.Values.ToList(), () => PotentialKindNames[pot.PotentialKind],
                    v => Set("Potansiyel türü", () => pot.PotentialKind, x => pot.PotentialKind = x, PotentialKindNames.First(k => k.Value == v).Key)));
                Rows.Add(new TextRow("Gerilim", () => pot.Voltage, v => Set("Gerilim", () => pot.Voltage, x => pot.Voltage = x, v.Trim())));
                Rows.Add(new TextRow("Açıklama", () => pot.Description, v => Set("Açıklama", () => pot.Description, x => pot.Description = x, v.Trim())));
                AddConnectorTransformRows(pot, Num);
                Rows.Add(new TextRow("Not", () => "Renk yalnızca görsel yardımdır; potansiyel adı ve türü ayrıca saklanır.", null));
                break;

            case PageLinkObject link:
                Header = $"Sayfa bağlantısı {link.Name}";
                Rows.Add(new TextRow("Ad", () => link.Name, v => Set("Bağlantı adı", () => link.Name, x => link.Name = x, v.Trim())));
                Rows.Add(new TextRow("Açıklama", () => link.Description, v => Set("Açıklama", () => link.Description, x => link.Description = x, v.Trim())));
                Rows.Add(new TextRow("Karşı uç", () => DescribeLinkTargets(link), null));
                Rows.Add(new TextRow("Potansiyel", () => DescribePotential(s.Network.PotentialOf(link.Pin.Ref)), null));
                AddConnectorTransformRows(link, Num);
                break;

            case SymbolObject sym:
                Header = "Sembol";
                Rows.Add(new TextRow("Sembol", () => s.Catalog.Find(sym.SymbolId)?.Name ?? sym.SymbolId, null));
                AddSymbolTransformRows(sym, Num);
                break;

            case LineObject l:
                Header = "Çizgi";
                Rows.Add(Num("X1", () => l.X1, v => l.X1 = v));
                Rows.Add(Num("Y1", () => l.Y1, v => l.Y1 = v));
                Rows.Add(Num("X2", () => l.X2, v => l.X2 = v));
                Rows.Add(Num("Y2", () => l.Y2, v => l.Y2 = v));
                Rows.Add(Num("Kalınlık", () => l.Thickness, v => l.Thickness = Math.Max(0.05, v)));
                Rows.Add(new NumberRow("Uzunluk", () => l.Start.DistanceTo(l.End), null));
                break;

            case RectangleObject r:
                Header = "Dikdörtgen";
                Rows.Add(Num("X", () => r.X, v => r.X = v));
                Rows.Add(Num("Y", () => r.Y, v => r.Y = v));
                Rows.Add(Num("Genişlik", () => r.Width, v => r.Width = Math.Max(0, v)));
                Rows.Add(Num("Yükseklik", () => r.Height, v => r.Height = Math.Max(0, v)));
                Rows.Add(Num("Kalınlık", () => r.Thickness, v => r.Thickness = Math.Max(0.05, v)));
                break;

            case TextObject t:
                Header = "Metin";
                Rows.Add(new TextRow("Metin", () => t.Text, v => Set("Metin", () => t.Text, x => t.Text = x, v)));
                Rows.Add(Num("X", () => t.X, v => t.X = v));
                Rows.Add(Num("Y", () => t.Y, v => t.Y = v));
                Rows.Add(Num("Yükseklik", () => t.Height, v => t.Height = Math.Max(0.5, v)));
                break;

            case ImageObject img:
                Header = "Resim";
                var source = s.Document.FindResource(img.SourceResourceId);
                Rows.Add(new TextRow("Kaynak", () => source?.FileName ?? "(bulunamadı)", null));
                Rows.Add(Num("X", () => img.X, v => img.X = v));
                Rows.Add(Num("Y", () => img.Y, v => img.Y = v));
                Rows.Add(Num("Genişlik", () => img.Width, v => img.Width = Math.Max(1, v)));
                Rows.Add(Num("Yükseklik", () => img.Height, v => img.Height = Math.Max(1, v)));
                Rows.Add(new TextRow("Saydamlık", () => img.TransparencyEnabled
                    ? string.Create(CultureInfo.CurrentCulture, $"#{img.TransparentColor & 0xFFFFFF:X6} · hassasiyet {img.Tolerance:0}")
                    : "Kapalı", null));
                Rows.Add(new ButtonRow("Resim ayarları…", () => editImage(img)));
                break;

            case JunctionObject j:
                Header = ShapeNames[j.Shape];
                Rows.Add(new ChoiceRow("Şekil", ShapeNames.Values.ToList(), () => ShapeNames[j.Shape],
                    v => Set("Şekil", () => j.Shape, x => j.Shape = x, ShapeNames.First(k => k.Value == v).Key)));
                Rows.Add(new ChoiceRow("Yön (°)", Rotations, () => j.Rotation.ToString(CultureInfo.InvariantCulture),
                    v => Set("Yön", () => j.Rotation, x => j.Rotation = x, double.Parse(v, CultureInfo.InvariantCulture))));
                Rows.Add(new TextRow("Bağlantılar", () => DescribeDirections(j), null));
                Rows.Add(Num("X", () => j.X, v => j.X = v));
                Rows.Add(Num("Y", () => j.Y, v => j.Y = v));
                Rows.Add(Num("Boyut", () => j.Size, v => j.Size = Math.Max(0.2, v)));
                Rows.Add(new BoolRow("Görünür", () => j.Visible, v => Set("Görünür", () => j.Visible, x => j.Visible = x, v)));
                break;
        }
    }

    private static readonly Dictionary<PotentialKind, string> PotentialKindNames = new()
    {
        [PotentialKind.Other] = "Diğer",
        [PotentialKind.L1] = "L1 (faz)",
        [PotentialKind.L2] = "L2 (faz)",
        [PotentialKind.L3] = "L3 (faz)",
        [PotentialKind.N] = "N (nötr)",
        [PotentialKind.PE] = "PE (koruma toprağı)",
        [PotentialKind.DcPositive] = "DC +",
        [PotentialKind.DcNegative] = "DC − / 0V",
        [PotentialKind.Signal] = "Sinyal",
        [PotentialKind.Communication] = "Haberleşme",
    };

    /// <summary>Ad değişince tür "Diğer" ise addan tahmin edilir (tek geri alma adımında iki değişiklik olmasın diye yalnız ad).</summary>
    private void SetPotentialName(PotentialObject pot, string name)
    {
        var s = _session!;
        s.SetProperty(pot, "Potansiyel adı", () => pot.Name, x => pot.Name = x, name);
        if (pot.PotentialKind == PotentialKind.Other && PotentialObject.GuessKind(name) is var kind && kind != PotentialKind.Other)
        {
            s.SetProperty(pot, "Potansiyel türü", () => pot.PotentialKind, x => pot.PotentialKind = x, kind);
        }
        Header = $"Potansiyel {pot.Name}";
    }

    private void AddConnectorTransformRows(SinglePinObject c, Func<string, Func<double>, Action<double>, NumberRow> num)
    {
        Rows.Add(new ChoiceRow("Yön (°)", Rotations, () => c.Rotation.ToString(CultureInfo.InvariantCulture),
            v => _session!.SetProperty(c, "Yön", () => c.Rotation, x => c.Rotation = x, double.Parse(v, CultureInfo.InvariantCulture))));
        Rows.Add(num("X", () => c.X, v => c.X = v));
        Rows.Add(num("Y", () => c.Y, v => c.Y = v));
    }

    private string DescribeLinkTargets(PageLinkObject link)
    {
        var targets = _session!.Network.LinkTargets(link);
        return targets.Count == 0 ? "Yok — diğer sayfaya aynı adla bir bağlantı koyun" : string.Join("  ", targets);
    }

    private static string DescribePotential(Core.Network.PotentialInfo? p) =>
        p is null ? "Tanımsız" : string.IsNullOrEmpty(p.Voltage) ? $"{p.Name} ({PotentialKindNames[p.Kind]})" : $"{p.Name} ({PotentialKindNames[p.Kind]}, {p.Voltage})";

    /// <summary>Proje cihaz kaydı (etikete bağlı, bütün parçalar için ortak) ve parçaların konumları.</summary>
    private void AddDeviceRecordRows(DeviceObject d)
    {
        var s = _session!;
        if (string.IsNullOrWhiteSpace(d.Reference))
        {
            return;
        }
        Rows.Add(new SectionRow($"Cihaz {d.Reference}"));
        TextRow Field(string label, Func<Core.Devices.DeviceRecord, string> get, Action<Core.Devices.DeviceRecord, string> set) =>
            new(label,
                () => Core.Devices.DeviceRegistry.Find(s.Document, d.Reference) is { } rec ? get(rec) : string.Empty,
                v =>
                {
                    if (Core.Devices.DeviceRegistry.Find(s.Document, d.Reference) is { } rec)
                    {
                        s.SetDocumentProperty(label, () => get(rec), x => set(rec, x), v.Trim());
                    }
                });
        Rows.Add(Field("Açıklama", r => r.Description, (r, v) => r.Description = v));
        Rows.Add(Field("Üretici", r => r.Manufacturer, (r, v) => r.Manufacturer = v));
        Rows.Add(Field("Model", r => r.Model, (r, v) => r.Model = v));
        Rows.Add(Field("Sipariş no", r => r.OrderNumber, (r, v) => r.OrderNumber = v));
        Rows.Add(Field("Yer", r => r.Location, (r, v) => r.Location = v));
        Rows.Add(new TextRow("Parçalar", () => DescribeParts(d.Reference), null));
    }

    private string DescribeParts(string reference)
    {
        var s = _session!;
        var parts = Core.Devices.DeviceRegistry.Placements(s.Document, reference)
            .Select(x =>
            {
                var name = s.Catalog.Find(x.Placement.SymbolId)?.Name ?? x.Placement.SymbolId;
                return $"{s.Network.LocationOf(x.Page, x.Placement.X)} {name}";
            });
        return string.Join("  ·  ", parts);
    }

    // ---------- Kablo ----------

    private void BuildWireRows()
    {
        var s = _session!;
        Header = "Kablo";
        Core.AutoWire.WireConnection? Wire() => s.SelectedWire;
        Core.Devices.WireData Data() => Wire() is { } w && s.ActivePage.FindWireData(w.From, w.To) is { } d ? d : new Core.Devices.WireData();
        void Update(Action<Core.Devices.WireData> change)
        {
            if (Wire() is { } w)
            {
                var copy = Data().Clone();
                change(copy);
                s.SetWireData(w, copy);
            }
        }
        Rows.Add(new TextRow("Kaynak", () => Wire() is { } w ? DescribeEnd(w.From) : "", null));
        Rows.Add(new TextRow("Hedef", () => Wire() is { } w ? DescribeEnd(w.To) : "", null));
        Rows.Add(new TextRow("Potansiyel", () => Wire() is { } w ? DescribePotential(s.Network.PotentialOf(w)) : "", null));
        Rows.Add(new SectionRow("Kablo bilgisi"));
        Rows.Add(new TextRow("Numara", () => Data().Number, v => Update(d => d.Number = v.Trim())));
        Rows.Add(new BoolRow("Numara kilitli", () => Data().NumberLocked, v => Update(d => d.NumberLocked = v)));
        Rows.Add(new TextRow("Damar rengi", () => Data().Color, v => Update(d => d.Color = v.Trim())));
        Rows.Add(new TextRow("Kesit", () => Data().CrossSection, v => Update(d => d.CrossSection = v.Trim())));
        Rows.Add(new TextRow("Kablo tipi", () => Data().CableType, v => Update(d => d.CableType = v.Trim())));
        Rows.Add(new TextRow("Açıklama", () => Data().Description, v => Update(d => d.Description = v.Trim())));
        Rows.Add(new TextRow("Not", () => "Numara kilitliyse otomatik numaralandırma değiştirmez. Kablo verisi iki uç pine bağlıdır.", null));
    }

    private string DescribeEnd(PinRef pin)
    {
        var obj = _session!.ActivePage.Find(pin.OwnerId);
        return obj switch
        {
            DeviceObject d => $"{(string.IsNullOrEmpty(d.Reference) ? "(etiketsiz)" : d.Reference)} : {d.PinLabel(pin.PinId)}",
            PotentialObject p => $"Potansiyel {p.Name}",
            PageLinkObject l => $"Sayfa bağlantısı {l.Name}",
            JunctionObject => "Bağlantı noktası",
            _ => pin.PinId,
        };
    }

    private void AddSymbolTransformRows(SymbolObject sym, Func<string, Func<double>, Action<double>, NumberRow> num)
    {
        Rows.Add(num("X", () => sym.X, v => sym.X = v));
        Rows.Add(num("Y", () => sym.Y, v => sym.Y = v));
        Rows.Add(new ChoiceRow("Dönüş (°)", Rotations,
            () => sym.Rotation.ToString(CultureInfo.InvariantCulture),
            v => _session!.SetProperty(sym, "Dönüş", () => sym.Rotation, x => sym.Rotation = x, double.Parse(v, CultureInfo.InvariantCulture))));
        Rows.Add(num("Ölçek", () => sym.Scale, v => sym.Scale = Math.Clamp(v, 0.1, 10)));
    }

    private static readonly Dictionary<JunctionShape, string> ShapeNames = new()
    {
        [JunctionShape.Angle] = "Dirsek",
        [JunctionShape.Tee] = "T bağlantı",
        [JunctionShape.Cross] = "Çapraz düğüm",
    };

    private static string DescribeDirections(JunctionObject j) => string.Join(" ", j.ActiveDirections.Select(d => d switch
    {
        PinDirection.Up => "↑",
        PinDirection.Down => "↓",
        PinDirection.Left => "←",
        _ => "→",
    }));

    private static string DescribePin(DeviceObject d, ConnectionPoint p)
    {
        var g = d.GetGlobalPins().First(x => x.PinId == p.Id);
        var dir = g.Direction switch
        {
            PinDirection.Up => "↑",
            PinDirection.Down => "↓",
            PinDirection.Left => "←",
            _ => "→",
        };
        return string.Create(CultureInfo.CurrentCulture, $"{dir}  X {g.Position.X:0.##}  Y {g.Position.Y:0.##}");
    }

    // ---------- Sayfa ----------

    private void BuildPageRows(Page page)
    {
        var s = _session!;
        Rows.Add(new SectionRow("Sayfa"));
        Rows.Add(new TextRow("Sayfa adı", () => page.Name,
            v => s.SetDocumentProperty("Sayfa adı", () => page.Name, x => page.Name = x, v.Trim(), page)));
        Rows.Add(new ChoiceRow("Tür", PageTypeNames.Values.ToList(), () => PageTypeNames[page.Type],
            v => s.SetDocumentProperty("Sayfa türü", () => page.Type, x => page.Type = x, PageTypeNames.First(k => k.Value == v).Key, page)));
        Rows.Add(new ChoiceRow("Format", Formats, () => page.Format.ToString(),
            v => s.SetDocumentProperty("Sayfa formatı", () => page.Format, x => page.SetFormat(x, page.Orientation), Enum.Parse<PageFormat>(v), page)));
        Rows.Add(new ChoiceRow("Yönelim", OrientationNames.Values.ToList(), () => OrientationNames[page.Orientation],
            v => s.SetDocumentProperty("Sayfa yönelimi", () => page.Orientation, x => page.SetFormat(page.Format, x), OrientationNames.First(k => k.Value == v).Key, page)));
        Rows.Add(new TextRow("Boyut (mm)", () => string.Create(CultureInfo.CurrentCulture, $"{page.Width:0} × {page.Height:0}"), null));
    }

    // ---------- Proje / kapak / antet ----------

    private void BuildProjectRows(ProjectInfo info)
    {
        var s = _session!;
        TextRow Field(string label, Func<string> get, Action<string> set) =>
            new(label, get, v => s.SetDocumentProperty(label, get, set, v.Trim()));

        Rows.Add(new SectionRow("Proje · Kapak · Antet"));
        Rows.Add(Field("Proje adı", () => info.Name, v => info.Name = v));
        Rows.Add(Field("Firma", () => info.Company, v => info.Company = v));
        Rows.Add(Field("Makine adı", () => info.MachineName, v => info.MachineName = v));
        Rows.Add(Field("Proje no", () => info.ProjectNumber, v => info.ProjectNumber = v));
        Rows.Add(Field("Müşteri", () => info.Customer, v => info.Customer = v));
        Rows.Add(Field("Hazırlayan", () => info.PreparedBy, v => info.PreparedBy = v));
        Rows.Add(Field("Kontrol eden", () => info.CheckedBy, v => info.CheckedBy = v));
        Rows.Add(Field("Tarih", () => info.Date, v => info.Date = v));
        Rows.Add(Field("Revizyon", () => info.Revision, v => info.Revision = v));
        Rows.Add(new FileRow("Logo", () => info.LogoPath,
            v =>
            {
                s.SetDocumentProperty("Logo", () => info.LogoPath, x => info.LogoPath = x, v);
                fileImagesChanged();
            },
            dialogs.OpenImageFile));
    }
}
