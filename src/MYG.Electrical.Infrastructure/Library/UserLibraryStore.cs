using System.Text.Json;
using System.Text.Json.Nodes;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Infrastructure.Library;

/// <summary>
/// Kullanıcı kütüphanesine yazma: çizimden oluşturulan sembol ve cihazlar
/// <c>%AppData%\MYG.Electrical\Symbols\kullanici.json</c> ve <c>...\Devices\kullanici.json</c> dosyalarına eklenir
/// (aynı kimlik varsa yerine yazılır). Dosya güvenli yazılır: geçici dosya + değiştirme, eski hali .bak.
/// </summary>
public sealed class UserLibraryStore(string rootFolder)
{
    public const string FileName = "kullanici.json";

    public string SymbolFile => Path.Combine(rootFolder, "Symbols", FileName);
    public string DeviceFile => Path.Combine(rootFolder, "Devices", FileName);

    public void Save(SymbolDefinition symbol, DeviceTemplate template)
    {
        Upsert(SymbolFile, "symbols", symbol.Id, JsonSerializer.SerializeToNode(symbol, SymbolLibraryLoader.JsonOptions)!);
        Upsert(DeviceFile, "devices", template.Id, JsonSerializer.SerializeToNode(template, SymbolLibraryLoader.JsonOptions)!);
    }

    private static void Upsert(string path, string arrayName, string id, JsonNode item)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonObject root;
        if (File.Exists(path))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                       ?? throw new JsonException("Kök nesne değil.");
            }
            catch (JsonException ex)
            {
                throw new IOException($"{path} okunamadı ({ex.Message}). Dosyayı düzeltin veya taşıyın; üzerine yazılmadı.", ex);
            }
        }
        else
        {
            root = new JsonObject { ["formatVersion"] = 1, ["library"] = "Kullanıcı", ["version"] = "1" };
        }
        if (root[arrayName] is not JsonArray array)
        {
            array = [];
            root[arrayName] = array;
        }
        var existing = array.FirstOrDefault(n => n is JsonObject o && string.Equals((string?)o["id"], id, StringComparison.Ordinal));
        if (existing is not null)
        {
            array[array.IndexOf(existing)] = item;
        }
        else
        {
            array.Add(item);
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(SymbolLibraryLoader.JsonOptions));
        if (File.Exists(path))
        {
            File.Replace(temp, path, path + ".bak");
        }
        else
        {
            File.Move(temp, path);
        }
    }
}
