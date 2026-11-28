using MYG.Electrical.App.Mvvm;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Veriden üretilen menü öğesi: başlık, komut ve parametre.</summary>
public sealed record MenuCommandItem(string Header, RelayCommand Command, object? Parameter);
