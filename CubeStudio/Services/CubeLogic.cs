using CubeStudio.Models;

namespace CubeStudio.Services;

public static class CubeLogic
{
    public static string DisplayName(CubeEntry card) => string.IsNullOrWhiteSpace(card.PrintedName) ? card.Name : card.PrintedName;

    public static int Count(CubeProject project) => project.Cards.Sum(c => c.Quantity);

    public static void Add(CubeProject project, CubeEntry card, int quantity = 1)
    {
        var existing = project.Cards.FirstOrDefault(c => c.Id == card.Id);
        if (existing is null)
        {
            card.Quantity = Math.Max(1, quantity);
            project.Cards.Add(card);
        }
        else existing.Quantity += Math.Max(1, quantity);
    }

    public static string Category(CubeEntry card)
    {
        if (card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase)) return "Länder";
        if (card.TypeLine.Contains("Token", StringComparison.OrdinalIgnoreCase)) return "Token";
        if (card.Colors.Count > 1) return "Mehrfarbig";
        if (card.Colors.Count == 0) return "Farblos";
        return card.Colors[0] switch { "W" => "Weiß", "U" => "Blau", "B" => "Schwarz", "R" => "Rot", "G" => "Grün", _ => "Farblos" };
    }

    public static string CardType(CubeEntry card)
    {
        var type = card.TypeLine;
        if (type.Contains("Creature", StringComparison.OrdinalIgnoreCase)) return "Kreaturen";
        if (type.Contains("Instant", StringComparison.OrdinalIgnoreCase)) return "Spontanzauber";
        if (type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase)) return "Hexereien";
        if (type.Contains("Enchantment", StringComparison.OrdinalIgnoreCase)) return "Verzauberungen";
        if (type.Contains("Artifact", StringComparison.OrdinalIgnoreCase)) return "Artefakte";
        return "Sonstiges";
    }

    // Einfache mathematische Obergrenze. Berücksichtigt noch keine Farb-/Bonus-Regeln.
    public static int SimpleBoosterBound(CubeProject p)
    {
        var r = p.BoosterRules;
        var groups = new[]
        {
            (r.Commons, p.Cards.Where(c => c.Rarity == "common").Sum(c => c.Quantity)),
            (r.Uncommons, p.Cards.Where(c => c.Rarity == "uncommon").Sum(c => c.Quantity)),
            (r.RareMythic, p.Cards.Where(c => c.Rarity is "rare" or "mythic").Sum(c => c.Quantity))
        };
        if (groups.All(g => g.Item1 == 0)) return 0;
        return groups.Where(g => g.Item1 > 0).Min(g => g.Item2 / g.Item1);
    }
}
