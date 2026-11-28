using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;

namespace MYG.Electrical.Drawing.Tests;

/// <summary>
/// Ana penceredeki panel düzeni kaydet/yükle yöntemiyle aynı adımlar (MainWindow.Docking.cs): düzen XML'e yazılır,
/// paneller kapatılıp geri yüklenince içerikler kimlikleriyle yeniden bağlanır ve gizli panel gizli kalır.
/// </summary>
public class DockingLayoutTests
{
    private static (Window Window, DockingManager Dock, Dictionary<string, object> Contents) Create()
    {
        var contents = new Dictionary<string, object>
        {
            ["explorer"] = new TextBlock { Text = "gezgin" },
            ["properties"] = new TextBlock { Text = "özellikler" },
            ["drc"] = new TextBlock { Text = "drc" },
            ["canvas"] = new Border(),
        };
        LayoutAnchorable A(string id) => new() { ContentId = id, Title = id, Content = contents[id], CanClose = false, CanHide = true };
        var root = new LayoutRoot();
        var panel = new LayoutPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new LayoutAnchorablePane(A("explorer")) { DockWidth = new GridLength(250) });
        var center = new LayoutPanel { Orientation = Orientation.Vertical };
        var docs = new LayoutDocumentPane();
        docs.Children.Add(new LayoutDocument { ContentId = "canvas", Title = "Çizim", Content = contents["canvas"], CanClose = false });
        center.Children.Add(docs);
        center.Children.Add(new LayoutAnchorablePane(A("drc")) { DockHeight = new GridLength(200) });
        panel.Children.Add(center);
        panel.Children.Add(new LayoutAnchorablePane(A("properties")) { DockWidth = new GridLength(300) });
        root.RootPanel = panel;
        var dock = new DockingManager { Layout = root, Theme = new AvalonDock.Themes.Vs2013DarkTheme() };
        var window = new Window { Content = dock, Width = 900, Height = 600, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -2000, Top = -2000 };
        return (window, dock, contents);
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Apply(DockingManager dock, Dictionary<string, object> contents, string xml)
    {
        foreach (var c in dock.Layout.Descendents().OfType<LayoutContent>())
        {
            c.Content = null;
        }
        var serializer = new XmlLayoutSerializer(dock);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            if (e.Model is LayoutContent m && m.ContentId is { } id && contents.TryGetValue(id, out var content))
            {
                e.Content = content;
            }
        };
        using var reader = new StringReader(xml);
        serializer.Deserialize(reader);
    }

    [Fact]
    public void Layout_round_trips_with_hidden_panel_and_reattached_content() => Sta.Run(() =>
    {
        var (window, dock, contents) = Create();
        window.Show();
        Flush();
        try
        {
            var drc = dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => a.ContentId == "drc");
            drc.Hide();
            Flush();

            using var writer = new StringWriter();
            new XmlLayoutSerializer(dock).Serialize(writer);
            var xml = writer.ToString();
            Assert.Contains("drc", xml, StringComparison.Ordinal);

            Apply(dock, contents, xml);
            Flush();

            var all = dock.Layout.Descendents().OfType<LayoutContent>().ToList();
            foreach (var (id, content) in contents)
            {
                Assert.Same(content, all.Single(c => c.ContentId == id).Content);
            }
            var restored = dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => a.ContentId == "drc");
            Assert.True(restored.IsHidden);
            restored.Show();
            Flush();
            Assert.False(restored.IsHidden);
        }
        finally
        {
            window.Close();
        }
    });
}
