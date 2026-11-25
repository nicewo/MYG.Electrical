namespace MYG.Electrical.Core.Editing;

/// <summary>Geri alınabilir düzenleme komutu.</summary>
public interface IEditCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>Belirli bir sayfadaki nesneleri değiştiren komut: geri alınırken o sayfaya geçilir.</summary>
public interface IPageScopedCommand
{
    Pages.Page? Page { get; }
}

/// <summary>Sayfa listesini (ekleme/silme/sıra) değiştiren komut.</summary>
public interface IPageListCommand
{
}
