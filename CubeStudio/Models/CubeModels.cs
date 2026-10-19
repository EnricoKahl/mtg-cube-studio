using System.Text.Json.Serialization;

namespace CubeStudio.Models;

public sealed class CubeProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Mein Cube";
    public string Type { get; set; } = "Mixed";
    public string SetCode { get; set; } = "";
    public string Language { get; set; } = "en";
    public int TargetSize { get; set; } = 360;
    public Dictionary<string, int> RarityMultipliers { get; set; } = new()
        { ["common"] = 4, ["uncommon"] = 2, ["rare"] = 1, ["mythic"] = 1 };
    public List<CubeEntry> Cards { get; set; } = new();
    public BoosterRules BoosterRules { get; set; } = new();
}

public sealed class CubeEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Set { get; set; } = "";
    public string CollectorNumber { get; set; } = "";
    public string Rarity { get; set; } = "";
    public string TypeLine { get; set; } = "";
    public string ManaCost { get; set; } = "";
    public double ManaValue { get; set; }
    public List<string> Colors { get; set; } = new();
    public string ImageUrl { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public sealed class BoosterRules
{
    public int Commons { get; set; } = 10;
    public int Uncommons { get; set; } = 3;
    public int RareMythic { get; set; } = 1;
    public int Bonus { get; set; } = 1;
    public bool OneCommonPerColor { get; set; } = true;
    public int Seed { get; set; } = 12345;
}

public sealed record MagicSet(string Code, string Name, string SetType, string ReleasedAt);
