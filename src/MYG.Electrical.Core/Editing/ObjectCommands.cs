using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Editing;

public sealed class AddObjectsCommand(Page page, IReadOnlyList<DrawingObject> objects) : IEditCommand, IPageScopedCommand
{
    public Page? Page => page;
    public string Description => objects.Count == 1 ? "Nesne ekle" : $"{objects.Count} nesne ekle";
    public IReadOnlyList<DrawingObject> Objects => objects;

    public void Execute() => page.Objects.AddRange(objects);

    public void Undo()
    {
        foreach (var o in objects)
        {
            page.Objects.Remove(o);
        }
    }
}

public sealed class RemoveObjectsCommand : IEditCommand, IPageScopedCommand
{
    public Page? Page => _page;
    private readonly Page _page;
    private readonly List<(int Index, DrawingObject Obj)> _removed;

    public RemoveObjectsCommand(Page page, IEnumerable<DrawingObject> objects)
    {
        _page = page;
        var set = objects.ToHashSet();
        _removed = page.Objects.Select((o, i) => (i, o)).Where(t => set.Contains(t.o)).ToList();
    }

    public string Description => _removed.Count == 1 ? "Nesne sil" : $"{_removed.Count} nesne sil";
    public IEnumerable<DrawingObject> Objects => _removed.Select(r => r.Obj);

    public void Execute()
    {
        foreach (var (_, obj) in _removed)
        {
            _page.Objects.Remove(obj);
        }
    }

    public void Undo()
    {
        foreach (var (index, obj) in _removed.OrderBy(r => r.Index))
        {
            _page.Objects.Insert(Math.Min(index, _page.Objects.Count), obj);
        }
    }
}

public sealed class MoveObjectsCommand(IReadOnlyList<DrawingObject> objects, double dx, double dy, Page? page = null) : IEditCommand, IPageScopedCommand
{
    public Page? Page => page;
    public string Description => "Taşı";
    public IReadOnlyList<DrawingObject> Objects => objects;

    public void Execute()
    {
        foreach (var o in objects)
        {
            o.Move(dx, dy);
        }
    }

    public void Undo()
    {
        foreach (var o in objects)
        {
            o.Move(-dx, -dy);
        }
    }
}

/// <summary>Seçili nesneleri kendi referans noktaları etrafında döndürür.</summary>
public sealed class RotateObjectsCommand(IReadOnlyList<DrawingObject> objects, double angle, Page? page = null) : IEditCommand, IPageScopedCommand
{
    public Page? Page => page;
    public string Description => "Döndür";
    public IReadOnlyList<DrawingObject> Objects => objects;

    public void Execute() => Apply(angle);
    public void Undo() => Apply(-angle);

    private void Apply(double a)
    {
        foreach (var o in objects.OfType<IRotatable>())
        {
            o.Rotation += a;
        }
    }
}

/// <summary>Tek bir özellik değişikliği.</summary>
public sealed class SetPropertyCommand<T>(string description, Action<T> setter, T oldValue, T newValue, DrawingObject? target = null, Page? page = null)
    : IEditCommand, IPageScopedCommand
{
    public Page? Page => page;
    public string Description => description;
    public DrawingObject? Target => target;
    public void Execute() => setter(newValue);
    public void Undo() => setter(oldValue);
}

/// <summary>Birden çok komutu tek adım olarak çalıştırır.</summary>
public sealed class CompositeCommand(string description, IReadOnlyList<IEditCommand> commands) : IEditCommand
{
    public string Description => description;

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
