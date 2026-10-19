using System.Net;
using System.Text.Json;
using CubeStudio.Models;

namespace CubeStudio.Services;

/// <summary>Gekapselter Zugriff auf Scryfall. Die Benutzeroberfläche kennt keine API-Details.</summary>
public sealed class ScryfallService : IDisposable
{
    private readonly HttpClient client = new();

    public ScryfallService()
    {
        client.BaseAddress = new Uri("https://api.scryfall.com/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MTGCubeStudio/0.1 (desktop cube builder)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.Timeout = TimeSpan.FromSeconds(35);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken token)
    {
        using var response = await client.GetAsync(path, token);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Keine passenden Karten gefunden.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }

    public async Task<List<MagicSet>> GetSetsAsync(CancellationToken token)
    {
        using var doc = await GetAsync("sets", token);
        var result = new List<MagicSet>();
        foreach (var set in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            string type = Read(set, "set_type");
            if (type is not ("expansion" or "core" or "masters" or "draft_innovation" or "funny")) continue;
            result.Add(new MagicSet(Read(set, "code"), Read(set, "name"), type, Read(set, "released_at")));
        }
        return result.OrderByDescending(s => s.ReleasedAt).ToList();
    }

    public async Task<List<CubeEntry>> GetSetCardsAsync(string setCode, IProgress<int>? progress, CancellationToken token)
    {
        // is:booster grenzt auf Karten ein, die in regulären Boostern erscheinen können.
        string path = "cards/search?q=" + Uri.EscapeDataString("set:" + setCode + " is:booster") + "&unique=prints&order=set";
        return await GetPagesAsync(path, progress, token);
    }

    public async Task<List<CubeEntry>> SearchCardsAsync(string query, CancellationToken token)
    {
        string path = "cards/search?q=" + Uri.EscapeDataString(query) + "&unique=cards";
        return await GetPagesAsync(path, null, token, maxPages: 1);
    }

    private async Task<List<CubeEntry>> GetPagesAsync(string path, IProgress<int>? progress, CancellationToken token, int maxPages = 30)
    {
        var cards = new List<CubeEntry>();
        for (int page = 0; page < maxPages; page++)
        {
            token.ThrowIfCancellationRequested();
            using var doc = await GetAsync(path, token);
            var root = doc.RootElement;
            foreach (var card in root.GetProperty("data").EnumerateArray())
                cards.Add(ParseCard(card));
            progress?.Report(cards.Count);
            if (!root.TryGetProperty("has_more", out var more) || !more.GetBoolean()) break;
            path = root.GetProperty("next_page").GetString()!;
            // Keine parallele Belastung der Scryfall-API.
            await Task.Delay(110, token);
        }
        return cards;
    }

    private static CubeEntry ParseCard(JsonElement card)
    {
        string image = "";
        if (card.TryGetProperty("image_uris", out var images)) image = Read(images, "normal");
        else if (card.TryGetProperty("card_faces", out var faces))
        {
            foreach (var face in faces.EnumerateArray())
            {
                if (face.TryGetProperty("image_uris", out var faceImages))
                { image = Read(faceImages, "normal"); break; }
            }
        }
        var colors = new List<string>();
        if (card.TryGetProperty("colors", out var cs) && cs.ValueKind == JsonValueKind.Array)
            colors = cs.EnumerateArray().Select(c => c.GetString() ?? "").ToList();
        return new CubeEntry
        {
            Id = Read(card, "id"), Name = Read(card, "name"), Set = Read(card, "set"),
            CollectorNumber = Read(card, "collector_number"), Rarity = Read(card, "rarity"),
            TypeLine = Read(card, "type_line"), ManaCost = Read(card, "mana_cost"),
            ManaValue = card.TryGetProperty("cmc", out var cmc) ? cmc.GetDouble() : 0,
            Colors = colors, ImageUrl = image, Quantity = 1
        };
    }

    private static string Read(JsonElement o, string key)
        => o.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    public void Dispose() => client.Dispose();
}
