using System.Text;
using System.Text.Json;
using CubeStudio.Models;

namespace CubeStudio.Services;

public static class ProjectService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static async Task SaveAsync(CubeProject project, string path)
    {
        string json = JsonSerializer.Serialize(project, Options);
        string temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, json, Encoding.UTF8);
        File.Move(temporary, path, true);
    }

    public static async Task<CubeProject> LoadAsync(string path)
    {
        var result = JsonSerializer.Deserialize<CubeProject>(await File.ReadAllTextAsync(path), Options)
            ?? throw new InvalidDataException("Die Projektdatei ist leer.");
        if (result.SchemaVersion != 1) throw new InvalidDataException("Diese Projektversion wird nicht unterstützt.");
        return result;
    }

    public static async Task ExportTxtAsync(CubeProject project, string path)
    {
        var lines = project.Cards.Where(x => x.Quantity > 0)
            .OrderBy(x => x.Name)
            .Select(x => $"{x.Quantity} {x.Name}");
        await File.WriteAllLinesAsync(path, lines, Encoding.UTF8);
    }
}
