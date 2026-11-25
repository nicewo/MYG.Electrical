using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Infrastructure.Serialization;

/// <summary>Açılan proje ve açılışta onarılan sorunlara ait kullanıcı uyarıları.</summary>
public sealed record ProjectLoadResult(ProjectDocument Document, IReadOnlyList<string> Warnings);
