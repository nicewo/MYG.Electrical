namespace MYG.Electrical.Core.Editing;

/// <summary>Komut tabanlı Geri Al / İleri Al yığını.</summary>
public sealed class UndoRedoManager
{
    public const int DefaultCapacity = 500;

    private readonly LinkedList<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();
    private readonly int _capacity;

    public UndoRedoManager(int capacity = DefaultCapacity) => _capacity = capacity;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoDescription => _undo.Last?.Value.Description;

    /// <summary>Sıradaki geri alınacak komut (çalıştırmadan).</summary>
    public IEditCommand? PeekUndo => _undo.Last?.Value;

    /// <summary>Sıradaki ileri alınacak komut (çalıştırmadan).</summary>
    public IEditCommand? PeekRedo => _redo.Count > 0 ? _redo.Peek() : null;
    public string? RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : null;

    /// <summary>Yığın değiştiğinde tetiklenir (UI komut durumları için).</summary>
    public event EventHandler? Changed;

    public void Execute(IEditCommand command)
    {
        command.Execute();
        Push(command);
    }

    /// <summary>Zaten uygulanmış bir değişikliği (ör. sürükleme sonrası taşıma) yığına ekler.</summary>
    public void Push(IEditCommand command)
    {
        _undo.AddLast(command);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveFirst();
        }
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Last is not { } node)
        {
            return;
        }
        _undo.RemoveLast();
        node.Value.Undo();
        _redo.Push(node.Value);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }
        var cmd = _redo.Pop();
        cmd.Execute();
        _undo.AddLast(cmd);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
