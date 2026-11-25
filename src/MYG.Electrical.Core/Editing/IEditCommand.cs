namespace MYG.Electrical.Core.Editing;

/// <summary>Geri alınabilir düzenleme komutu.</summary>
public interface IEditCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}
