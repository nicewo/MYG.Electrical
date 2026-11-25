using MYG.Electrical.App.Mvvm;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.App.ViewModels;

public sealed class PageItemViewModel(Page page, int number) : ObservableObject
{
    public Page Page { get; } = page;
    public int Number { get; } = number;
    public string Name => Page.Name;
    public string Kind => Page.Type == PageType.Cover ? "Kapak" : "Şema";
    public string Display => $"{Number}. {Page.Name}";
    public string Details => $"{Kind} · {Page.Format} {(Page.Orientation == PageOrientation.Landscape ? "yatay" : "dikey")}";

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Kind));
    }
}

/// <summary>Proje ağacının kök düğümü.</summary>
public sealed class ProjectNodeViewModel(string name, IEnumerable<PageItemViewModel> pages) : ObservableObject
{
    public string Name { get; } = name;
    public IEnumerable<PageItemViewModel> Pages { get; } = pages;
}
