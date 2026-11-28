using System.IO;
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using MYG.Electrical.App.Services;

namespace MYG.Electrical.App.Views;

/// <summary>
/// Yerleştirilebilir paneller (Visual Studio "Çözüm Gezgini" gibi): kapatılır, Görünüm → Paneller'den açılır,
/// sürüklenip başka kenara/sekmeye yerleştirilir, ayrı pencere olarak yüzdürülür, otomatik gizlenir.
/// Düzen %AppData%\MYG.Electrical\layout.xml dosyasında saklanır; bozuksa veya eksik panel varsa varsayılan kullanılır.
/// </summary>
public partial class MainWindow
{
    private static readonly string LayoutPath = Path.Combine(AppSettingsService.AppDataFolder, "layout.xml");
    private readonly Dictionary<string, object> _panelContents = [];
    private string? _defaultLayout;

    /// <summary>Panel içeriklerini kimlikle saklar ve DataContext'i doğrudan verir (oluşturucuda, bağlamalar hemen çalışsın).</summary>
    private void CollectPanels()
    {
        foreach (var content in Dock.Layout.Descendents().OfType<LayoutContent>())
        {
            if (content.ContentId is { } id && content.Content is FrameworkElement fe)
            {
                _panelContents[id] = fe;
                // Yüzen (ayrı pencere) paneller ana pencerenin DataContext'ini miras almaz
                fe.DataContext = _vm;
            }
        }
    }

    /// <summary>Varsayılan düzeni hafızaya alır, kayıtlı düzeni yükler.</summary>
    private void InitializeDocking()
    {
        _defaultLayout = SerializeLayout();
        if (File.Exists(LayoutPath))
        {
            try
            {
                ApplyLayout(File.ReadAllText(LayoutPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Xml.XmlException or FormatException)
            {
                Serilog.Log.Warning(ex, "Panel düzeni yüklenemedi, varsayılan kullanılıyor");
                ApplyLayout(_defaultLayout);
            }
        }
        _vm.BottomPanel.ShowRequested += (_, id) => ShowPanel(id);
    }

    private string SerializeLayout()
    {
        using var writer = new StringWriter();
        new XmlLayoutSerializer(Dock).Serialize(writer);
        return writer.ToString();
    }

    /// <summary>Düzeni uygular; bilinen panellerden biri eksik kalırsa varsayılana döner (yeni sürümde eklenen panel kaybolmasın).</summary>
    private void ApplyLayout(string xml)
    {
        foreach (var content in Dock.Layout.Descendents().OfType<LayoutContent>())
        {
            content.Content = null; // içerik yeni düzene taşınabilsin (tek görsel ebeveyn)
        }
        var serializer = new XmlLayoutSerializer(Dock);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            if (e.Model is LayoutContent model && model.ContentId is { } id && _panelContents.TryGetValue(id, out var content))
            {
                e.Content = content;
            }
        };
        using (var reader = new StringReader(xml))
        {
            serializer.Deserialize(reader);
        }
        var present = Dock.Layout.Descendents().OfType<LayoutContent>().Where(c => c.Content is not null).Select(c => c.ContentId).ToHashSet();
        if (_panelContents.Keys.Any(k => !present.Contains(k)) && !ReferenceEquals(xml, _defaultLayout) && _defaultLayout is not null)
        {
            ApplyLayout(_defaultLayout);
        }
    }

    private void SaveLayout()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            File.WriteAllText(LayoutPath, SerializeLayout());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "Panel düzeni kaydedilemedi");
        }
    }

    private LayoutAnchorable? FindPanel(string id) =>
        Dock.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == id);

    private void ShowPanel(string id)
    {
        if (FindPanel(id) is not { } panel)
        {
            return;
        }
        if (panel.IsHidden)
        {
            panel.Show();
        }
        panel.IsSelected = true;
        if (panel.IsAutoHidden)
        {
            panel.IsActive = true;
        }
    }

    private void OnTogglePanel(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string id } || FindPanel(id) is not { } panel)
        {
            return;
        }
        if (panel.IsHidden || !panel.IsVisible)
        {
            ShowPanel(id);
        }
        else
        {
            panel.Hide();
        }
    }

    private void OnPanelsMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menu)
        {
            return;
        }
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is string id && FindPanel(id) is { } panel)
            {
                item.IsChecked = !panel.IsHidden && panel.IsVisible;
            }
        }
    }

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        if (_defaultLayout is not null)
        {
            ApplyLayout(_defaultLayout);
        }
    }
}
