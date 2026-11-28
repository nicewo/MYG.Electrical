using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Serialization;

namespace MYG.Electrical.Infrastructure.Library;

/// <summary>
/// Kapak şablonları: <c>%AppData%\MYG.Electrical\Templates\Cover\&lt;ad&gt;.mygk</c>. Yerleşik "Standart" şablon
/// dosyada tutulmaz, her zaman listededir. Dosya güvenli yazılır (geçici dosya + değiştirme).
/// </summary>
public sealed class CoverTemplateStore(string rootFolder)
{
    public const string Extension = ".mygk";

    public string Folder => Path.Combine(rootFolder, "Templates", "Cover");

    /// <summary>Şablon adları: önce Standart, sonra kullanıcı şablonları alfabetik.</summary>
    public IReadOnlyList<string> Names()
    {
        var names = new List<string> { CoverTemplate.StandardName };
        if (Directory.Exists(Folder))
        {
            names.AddRange(Directory.EnumerateFiles(Folder, "*" + Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(n => !IsStandard(n))
                .Order(StringComparer.CurrentCultureIgnoreCase));
        }
        return names;
    }

    public static bool IsStandard(string name) => string.Equals(name.Trim(), CoverTemplate.StandardName, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Şablonu okur; Standart, bulunamayan ya da bozuk şablon için Standart döner (hata mesajıyla).</summary>
    public (CoverTemplate Template, string? Error) Load(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || IsStandard(name))
        {
            return (CoverTemplate.Standard(), null);
        }
        var path = PathOf(name);
        if (!File.Exists(path))
        {
            return (CoverTemplate.Standard(), $"'{name}' kapak şablonu bulunamadı; Standart kullanıldı.");
        }
        try
        {
            var template = ProjectSerializer.DeserializeCoverTemplate(File.ReadAllText(path));
            template.Name = name;
            return (template, null);
        }
        catch (Exception ex) when (ex is ProjectFormatException or IOException or UnauthorizedAccessException)
        {
            return (CoverTemplate.Standard(), $"'{name}' kapak şablonu okunamadı ({ex.Message}); Standart kullanıldı.");
        }
    }

    public void Save(CoverTemplate template)
    {
        ValidateName(template.Name);
        Directory.CreateDirectory(Folder);
        var path = PathOf(template.Name);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ProjectSerializer.SerializeCoverTemplate(template));
        if (File.Exists(path))
        {
            File.Replace(tmp, path, null);
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    public void Delete(string name)
    {
        if (!IsStandard(name) && File.Exists(PathOf(name)))
        {
            File.Delete(PathOf(name));
        }
    }

    public bool Exists(string name) => IsStandard(name) || File.Exists(PathOf(name));

    /// <summary>Ad boş, Standart ya da dosya adında geçersiz karakter içeriyorsa açıklamalı hata.</summary>
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Şablon adı boş olamaz.");
        }
        if (IsStandard(name))
        {
            throw new ArgumentException($"'{CoverTemplate.StandardName}' yerleşik şablonun adı; başka bir ad seçin.");
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0)
        {
            throw new ArgumentException("Şablon adında \\ / : * ? \" < > | karakterleri kullanılamaz.");
        }
    }

    private string PathOf(string name) => Path.Combine(Folder, name.Trim() + Extension);
}
