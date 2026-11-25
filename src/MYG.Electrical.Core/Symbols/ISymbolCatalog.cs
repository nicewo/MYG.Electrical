namespace MYG.Electrical.Core.Symbols;

/// <summary>Sembol tanımlarına erişim.</summary>
public interface ISymbolCatalog
{
    IReadOnlyList<SymbolDefinition> All { get; }
    SymbolDefinition? Find(string symbolId);
}
