using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Editing;

/// <summary>Proje cihaz kayıtlarını ekler (yeni etiketli yerleşimle birlikte; geri alınınca kayıt da kalkar).</summary>
public sealed class AddDeviceRecordsCommand(ProjectDocument document, IReadOnlyList<DeviceRecord> records) : IEditCommand
{
    public string Description => "Cihaz kaydı ekle";

    public void Execute() => document.Devices.AddRange(records);

    public void Undo()
    {
        foreach (var r in records)
        {
            document.Devices.Remove(r);
        }
    }
}

/// <summary>Proje cihaz kayıtlarını kaldırır (geri alınınca aynı sıraya döner).</summary>
public sealed class RemoveDeviceRecordsCommand(ProjectDocument document, IReadOnlyList<DeviceRecord> records) : IEditCommand
{
    private readonly List<(int Index, DeviceRecord Record)> _removed = [];

    public string Description => "Cihaz kaydı kaldır";

    public void Execute()
    {
        _removed.Clear();
        foreach (var r in records)
        {
            var i = document.Devices.IndexOf(r);
            if (i >= 0)
            {
                _removed.Add((i, r));
                document.Devices.RemoveAt(i);
            }
        }
    }

    public void Undo()
    {
        for (var k = _removed.Count - 1; k >= 0; k--)
        {
            var (i, r) = _removed[k];
            document.Devices.Insert(Math.Min(i, document.Devices.Count), r);
        }
    }
}

/// <summary>Sayfaya bağlı bileşik komut: geri alırken ilgili sayfaya geçilir.</summary>
public sealed class PageCompositeCommand(string description, Page page, IReadOnlyList<IEditCommand> commands) : IEditCommand, IPageScopedCommand
{
    public Page? Page => page;
    public string Description => description;
    public IReadOnlyList<IEditCommand> Commands => commands;

    public void Execute()
    {
        foreach (var c in commands)
        {
            c.Execute();
        }
    }

    public void Undo()
    {
        for (var i = commands.Count - 1; i >= 0; i--)
        {
            commands[i].Undo();
        }
    }
}

/// <summary>Sayfaların kablo verisi listelerini topluca değiştirir (numaralandırma, kablo özelliği düzenleme).</summary>
public sealed class SetWireDataCommand : IEditCommand, IPageScopedCommand
{
    private readonly List<(Page Page, List<WireData> Old, List<WireData> New)> _changes;

    public SetWireDataCommand(string description, IReadOnlyDictionary<Page, List<WireData>> newLists)
    {
        Description = description;
        _changes = newLists.Select(kv => (kv.Key, kv.Key.WireData, kv.Value)).ToList();
    }

    public string Description { get; }

    /// <summary>Tek sayfalık değişiklikte geri alırken o sayfaya geçilir.</summary>
    public Page? Page => _changes.Count == 1 ? _changes[0].Page : null;

    public bool IsEmpty => _changes.Count == 0;

    public void Execute()
    {
        foreach (var (page, _, n) in _changes)
        {
            page.WireData = n;
        }
    }

    public void Undo()
    {
        foreach (var (page, old, _) in _changes)
        {
            page.WireData = old;
        }
    }
}

/// <summary>Proje cihaz kaydının içeriğini topluca değiştirir (cihaz özellikleri penceresi).</summary>
public sealed class ReplaceDeviceRecordCommand(ProjectDocument document, DeviceRecord current, DeviceRecord replacement) : IEditCommand
{
    public string Description => "Cihaz özellikleri";

    public void Execute() => Swap(current, replacement);

    public void Undo() => Swap(replacement, current);

    private void Swap(DeviceRecord from, DeviceRecord to)
    {
        var i = document.Devices.IndexOf(from);
        if (i >= 0)
        {
            document.Devices[i] = to;
        }
        else
        {
            document.Devices.Add(to);
        }
    }
}
