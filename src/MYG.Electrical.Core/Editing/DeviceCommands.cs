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
