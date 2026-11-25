using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Editing;

public sealed class AddPageCommand(ProjectDocument document, Page page, int index) : IEditCommand, IPageScopedCommand, IPageListCommand
{
    public string Description => "Sayfa ekle";
    public Page? Page => page;

    public void Execute() => document.Pages.Insert(Math.Clamp(index, 0, document.Pages.Count), page);
    public void Undo() => document.Pages.Remove(page);
}

public sealed class RemovePageCommand : IEditCommand, IPageScopedCommand, IPageListCommand
{
    private readonly ProjectDocument _document;
    private readonly Page _page;
    private readonly int _index;

    public RemovePageCommand(ProjectDocument document, Page page)
    {
        _document = document;
        _page = page;
        _index = document.Pages.IndexOf(page);
    }

    public string Description => "Sayfa sil";

    /// <summary>Geri alınınca silinen sayfa geri gelir ve ona geçilir.</summary>
    public Page? Page => _page;

    public void Execute() => _document.Pages.Remove(_page);
    public void Undo() => _document.Pages.Insert(Math.Clamp(_index, 0, _document.Pages.Count), _page);
}

public sealed class MovePageCommand(ProjectDocument document, Page page, int from, int to) : IEditCommand, IPageScopedCommand, IPageListCommand
{
    public string Description => "Sayfa taşı";
    public Page? Page => page;

    public void Execute() => Move(to);
    public void Undo() => Move(from);

    private void Move(int target)
    {
        document.Pages.Remove(page);
        document.Pages.Insert(Math.Clamp(target, 0, document.Pages.Count), page);
    }
}
