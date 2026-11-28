using System.Collections.ObjectModel;
using System.Globalization;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Drc;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Alt paneldeki mesaj satırı.</summary>
public sealed record MessageRow(string Time, string Text);

/// <summary>Çapraz referans tablosu satırı (cihaz başına).</summary>
public sealed record CrossReferenceRow(string Reference, string Description, string Parts, string Locations);

/// <summary>
/// Alt panel: DRC sonuçları, mesajlar, çapraz referanslar ve proje bilgisi. DRC bulgularındaki nesneler çizimde
/// kırmızı vurgulanır; bulguya çift tıklayınca ilgili sayfaya geçilip nesneler seçilir.
/// </summary>
public sealed class BottomPanelViewModel : ObservableObject
{
    private const int MaxMessages = 300;
    private readonly Func<EditorSession> _session;
    private DrcFinding? _selectedFinding;
    private IReadOnlyCollection<Guid>? _highlighted;
    private string _drcSummary = "DRC henüz çalıştırılmadı.";
    private bool _isOpen = true;
    private int _selectedTab;

    public BottomPanelViewModel(Func<EditorSession> session)
    {
        _session = session;
        RunDrcCommand = new RelayCommand(RunDrc);
        ClearDrcCommand = new RelayCommand(ClearDrc);
        RefreshCommand = new RelayCommand(RefreshProjectViews);
        ClearMessagesCommand = new RelayCommand(Messages.Clear);
        GoToFindingCommand = new RelayCommand(p => GoTo(p as DrcFinding ?? SelectedFinding));
        ToggleCommand = new RelayCommand(() => IsOpen = !IsOpen);
    }

    public ObservableCollection<DrcFinding> Findings { get; } = [];
    public ObservableCollection<MessageRow> Messages { get; } = [];
    public ObservableCollection<CrossReferenceRow> CrossReferences { get; } = [];
    public ObservableCollection<KeyValueRow> ProjectInfo { get; } = [];

    public RelayCommand RunDrcCommand { get; }
    public RelayCommand ClearDrcCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ClearMessagesCommand { get; }
    public RelayCommand GoToFindingCommand { get; }
    public RelayCommand ToggleCommand { get; }

    public string Disclaimer => DrcEngine.Disclaimer;

    /// <summary>Bir alt panelin görünür ve önde olması istendi (ör. DRC çalışınca); panel kimliği.</summary>
    public event EventHandler<string>? ShowRequested;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetField(ref _isOpen, value);
    }

    public int SelectedTab
    {
        get => _selectedTab;
        set => SetField(ref _selectedTab, value);
    }

    public string DrcSummary
    {
        get => _drcSummary;
        private set => SetField(ref _drcSummary, value);
    }

    public DrcFinding? SelectedFinding
    {
        get => _selectedFinding;
        set => SetField(ref _selectedFinding, value);
    }

    /// <summary>Çizimde kırmızı vurgulanacak nesneler (bütün bulgular).</summary>
    public IReadOnlyCollection<Guid>? Highlighted
    {
        get => _highlighted;
        private set => SetField(ref _highlighted, value);
    }

    public void AddMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        Messages.Insert(0, new MessageRow(DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture), text));
        while (Messages.Count > MaxMessages)
        {
            Messages.RemoveAt(Messages.Count - 1);
        }
    }

    public void RunDrc()
    {
        var s = _session();
        var findings = DrcEngine.Run(s.Document, s.Network, s.Catalog);
        Findings.Clear();
        foreach (var f in findings)
        {
            Findings.Add(f);
        }
        var errors = findings.Count(f => f.Severity == DrcSeverity.Error);
        var warnings = findings.Count(f => f.Severity == DrcSeverity.Warning);
        var infos = findings.Count - errors - warnings;
        DrcSummary = findings.Count == 0
            ? "DRC: bulgu yok."
            : $"DRC: {errors} hata, {warnings} uyarı, {infos} bilgi.";
        Highlighted = findings.Where(f => f.Severity != DrcSeverity.Info).SelectMany(f => f.ObjectIds).ToHashSet();
        AddMessage(DrcSummary);
        SelectedTab = 0;
        IsOpen = true;
        ShowRequested?.Invoke(this, "drc");
        RefreshProjectViews();
    }

    public void ClearDrc()
    {
        Findings.Clear();
        Highlighted = null;
        DrcSummary = "DRC temizlendi.";
    }

    /// <summary>Proje değişti (yeni/aç): eski bulgular geçersiz.</summary>
    public void OnSessionChanged()
    {
        ClearDrc();
        DrcSummary = "DRC henüz çalıştırılmadı.";
        RefreshProjectViews();
    }

    private void GoTo(DrcFinding? finding)
    {
        if (finding?.Page is not { } page)
        {
            return;
        }
        var s = _session();
        if (!s.Document.Pages.Contains(page))
        {
            return;
        }
        s.ActivePage = page;
        s.SelectMany(page.Objects.Where(o => finding.ObjectIds.Contains(o.Id)));
    }

    public void RefreshProjectViews()
    {
        var s = _session();
        var doc = s.Document;
        CrossReferences.Clear();
        foreach (var rec in doc.Devices.OrderBy(r => r.Reference, Core.Reports.NaturalComparer.Instance))
        {
            var parts = Core.Network.CrossReferences.Parts(s.Network, s.Catalog, rec.Reference);
            if (parts.Count == 0)
            {
                continue;
            }
            CrossReferences.Add(new CrossReferenceRow(rec.Reference, rec.Description,
                string.Join(" · ", parts.Select(p => $"{p.Symbol?.Name ?? p.Placement.SymbolId} ({p.Paths})")),
                string.Join(" ", parts.Select(p => p.Location.ToString()))));
        }

        ProjectInfo.Clear();
        var objects = doc.Pages.SelectMany(p => p.Objects).ToList();
        var wires = s.Network.Wires.Values.Sum(w => w.Count);
        var numbered = doc.Pages.Sum(p => p.WireData.Count(w => w.Number.Length > 0));
        void Add(string k, string v) => ProjectInfo.Add(new KeyValueRow(k, v));
        Add("Proje", doc.Project.Name);
        Add("Makine", doc.Project.MachineName);
        Add("Sayfa", doc.Pages.Count.ToString(CultureInfo.CurrentCulture));
        Add("Cihaz (etiket)", doc.Devices.Count(r => DeviceRegistry.Placements(doc, r.Reference).Any()).ToString(CultureInfo.CurrentCulture));
        Add("Sembol yerleşimi", objects.OfType<DeviceObject>().Count().ToString(CultureInfo.CurrentCulture));
        Add("Kablo (numaralı)", $"{wires} ({numbered})");
        Add("Potansiyel", string.Join(", ", objects.OfType<PotentialObject>().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase)));
        Add("Sayfa bağlantısı", objects.OfType<PageLinkObject>().Count().ToString(CultureInfo.CurrentCulture));
        Add("Etiket şablonu", doc.Settings.ReferenceFormat);
        Add("Kablo no şablonu", doc.Settings.WireNumberFormat);
        Add("Dosya biçimi", ProjectDocument.CurrentFormatVersion.ToString(CultureInfo.InvariantCulture));
        Add("Sembol kütüphanesi", s.Catalog.All.FirstOrDefault(x => !x.IsUserDefined)?.Library ?? "");
        Add("Kullanıcı sembolü", s.Catalog.All.Count(x => x.IsUserDefined).ToString(CultureInfo.CurrentCulture));
    }
}
