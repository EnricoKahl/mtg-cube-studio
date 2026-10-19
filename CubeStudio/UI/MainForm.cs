using System.Drawing.Drawing2D;
using CubeStudio.Models;
using CubeStudio.Services;

namespace CubeStudio.UI;

public sealed class MainForm : Form
{
    private readonly ScryfallService scryfall = new();
    private CubeProject project = new();
    private string? projectPath;
    private bool dirty;
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly TextBox nameBox = new() { Width = 280 };
    private readonly ComboBox typeBox = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox languageBox = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown targetBox = new() { Minimum = 1, Maximum = 100000, Value = 360, Width = 90 };
    private readonly ComboBox setsBox = new() { Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Dictionary<string, NumericUpDown> rarityInputs = new();
    private readonly TextBox searchBox = new() { Width = 330, PlaceholderText = "Englischen oder deutschen Kartennamen eingeben" };
    private readonly NumericUpDown addQuantity = new() { Minimum = 1, Maximum = 100, Value = 1, Width = 70 };
    private readonly ListBox searchResults = new() { Dock = DockStyle.Fill, DisplayMember = nameof(CubeEntry.Name) };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly CubeCanvas gallery = new() { Dock = DockStyle.Fill };
    private readonly ComboBox viewBox = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label summary = new() { AutoSize = true };
    private readonly Label status = new() { AutoSize = true, Text = "Bereit" };
    private readonly PictureBox preview = new() { Width = 245, Height = 345, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(35, 37, 42) };
    private readonly HttpClient imageClient = new();
    private readonly Dictionary<string, Image> images = new();
    private CancellationTokenSource? operation;
    private bool syncing;
    private readonly Button cancelButton = new() { Text = "Abbrechen", Enabled = false, AutoSize = true };
    private readonly List<MagicSet> availableSets = new();

    public MainForm()
    {
        Text = "MTG Cube Studio v0.2";
        Width = 1330; Height = 850; MinimumSize = new Size(960, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 0, 0) };
        header.Controls.AddRange(new Control[] { Btn("Neu", async (_, _) => await NewProjectAsync()), Btn("Öffnen", async (_, _) => await OpenProjectAsync()), Btn("Speichern", async (_, _) => await SaveProjectAsync()), Btn("Speichern unter", async (_, _) => await SaveProjectAsync(true)), cancelButton, summary });
        cancelButton.Click += (_, _) => operation?.Cancel();
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(12, 3, 0, 0) };
        bottom.Controls.Add(status);
        Controls.Add(tabs); Controls.Add(bottom); Controls.Add(header);
        typeBox.Items.AddRange(new object[] { "Set", "Mixed" });
        languageBox.Items.AddRange(new object[] { "Englisch", "Deutsch" });
        viewBox.Items.AddRange(new object[] { "Tabelle", "Kartenraster", "Stapel" });
        gallery.CardHovered += async card => await ShowImageAsync(card);
        gallery.QuantityChanged += _ => MarkChanged();
        MakeProjectTab(); MakeSetTab(); MakeSearchTab(); MakeEditorTab(); MakeExportTab();
        typeBox.SelectedIndexChanged += (_, _) => { if (syncing) return; project.Type = typeBox.SelectedItem?.ToString() ?? "Mixed"; MarkChanged(); };
        languageBox.SelectedIndexChanged += (_, _) => { if (syncing) return; project.Language = languageBox.SelectedIndex == 1 ? "de" : "en"; MarkChanged(); status.Text = "Sprache geändert: vorhandene Karten über Druckversionen aktualisieren neu laden."; };
        targetBox.ValueChanged += (_, _) => { if (syncing) return; project.TargetSize = (int)targetBox.Value; MarkChanged(); };
        nameBox.TextChanged += (_, _) => { if (syncing) return; project.Name = nameBox.Text; MarkChanged(); };
        viewBox.SelectedIndexChanged += (_, _) => RefreshEditor();
        searchResults.SelectedIndexChanged += async (_, _) =>
        {
            if (searchResults.SelectedItem is CubeEntry item) await ShowImageAsync(item);
        };
        grid.SelectionChanged += async (_, _) =>
        {
            if (grid.CurrentRow?.Tag is CubeEntry item) await ShowImageAsync(item);
        };
        FormClosing += OnClosing;
        SyncControls();
        Shown += async (_, _) => await LoadSetsAsync();
    }

    private static Button Btn(string title, EventHandler handler)
    {
        var button = new Button { Text = title, AutoSize = true, Height = 30, Margin = new Padding(3) };
        button.Click += handler;
        return button;
    }

    private static FlowLayoutPanel Row() => new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(12), FlowDirection = FlowDirection.LeftToRight };
    private static Label L(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(7, 8, 7, 4) };
    private static TabPage Page(string title) => new(title) { Padding = new Padding(8) };

    private void MakeProjectTab()
    {
        var page = Page("1 · Projekt");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20), AutoScroll = true };
        var row1 = Row(); row1.Controls.AddRange(new Control[] { L("Projektname:"), nameBox });
        var row2 = Row(); row2.Controls.AddRange(new Control[] { L("Cube-Typ:"), typeBox, L("Kartensprache:"), languageBox, L("Zielgröße (Mixed):"), targetBox });
        var languageRow = Row(); languageRow.Controls.Add(Btn("Druckversionen aktualisieren (Sprache übernehmen)", async (_, _) => await RelocalizeAsync()));
        panel.Controls.AddRange(new Control[] { L("Willkommen! Lege einen Set-Cube an oder füge einem Mixed-Cube Karten hinzu."), row1, row2, languageRow,
            L("Set-Cube: Größe entsteht aus dem Set und Seltenheitsmultiplikatoren. Mixed-Cube: Zielgröße ist nur ein Richtwert."),
            L("Projektdateien werden lokal als .cube.json gespeichert. Der Original-Cube wird nie durch Simulationen verändert.") });
        page.Controls.Add(panel); tabs.TabPages.Add(page);
    }

    private void MakeSetTab()
    {
        var page = Page("2 · Set-Cube");
        var container = new Panel { Dock = DockStyle.Fill };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(14) };
        var setrow = Row(); setrow.Controls.AddRange(new Control[] { L("Magic-Set:"), setsBox, Btn("Sets aktualisieren", async (_, _) => await LoadSetsAsync()) });
        stack.Controls.Add(setrow); stack.Controls.Add(L("Exemplare je Karte nach Seltenheit:"));
        foreach (var (rarity, label) in new[] { ("common", "Common"), ("uncommon", "Uncommon"), ("rare", "Rare"), ("mythic", "Mythic") })
        {
            var row = Row();
            var input = new NumericUpDown { Minimum = 0, Maximum = 100, Width = 75 };
            rarityInputs[rarity] = input;
            input.ValueChanged += (_, _) => { if (syncing) return; project.RarityMultipliers[rarity] = (int)input.Value; MarkChanged(); };
            row.Controls.AddRange(new Control[] { L(label), input }); stack.Controls.Add(row);
        }
        stack.Controls.Add(Btn("Set-Cube generieren (ersetzt derzeitigen Kartenpool)", async (_, _) => await GenerateSetAsync()));
        stack.Controls.Add(L("Hinweis: Scryfall is:booster filtert Booster-Karten; einzelne Set-Sonderfälle müssen später genauer geprüft werden."));
        container.Controls.Add(stack); page.Controls.Add(container); tabs.TabPages.Add(page);
    }

    private void MakeSearchTab()
    {
        var page = Page("3 · Kartensuche");
        var header = Row();
        header.Controls.AddRange(new Control[] { searchBox, Btn("Scryfall suchen", async (_, _) => await SearchAsync()), L("Anzahl:"), addQuantity, Btn("Auswahl hinzufügen", async (_, _) => await AddSelectedAsync()) });
        searchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 650 };
        split.Panel1.Controls.Add(searchResults);
        split.Panel2.Controls.Add(new Label { Text = "Bildvorschau", Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter });
        var pic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(35, 37, 42) };
        split.Panel2.Controls.Add(pic); pic.BringToFront();
        searchResults.SelectedIndexChanged += async (_, _) => { if (searchResults.SelectedItem is CubeEntry c) await ShowImageAsync(c, pic); };
        page.Controls.Add(split); page.Controls.Add(header); tabs.TabPages.Add(page);
    }

    private void MakeEditorTab()
    {
        var page = Page("4 · Cube-Editor");
        var top = Row(); top.Controls.AddRange(new Control[] { L("Ansicht:"), viewBox, Btn("+1", (_, _) => AdjustSelected(1)), Btn("−1", (_, _) => AdjustSelected(-1)), Btn("Entfernen", (_, _) => RemoveSelected()), Btn("Aktualisieren", (_, _) => RefreshEditor()) });
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 940 };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Anzahl", Width = 65 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kartenname", Width = 270 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Farbe", Width = 105 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Typ", Width = 125 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Seltenheit", Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Set", Width = 65 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mana", Width = 75 });
        grid.ReadOnly = true;
        split.Panel1.Controls.Add(grid); split.Panel1.Controls.Add(gallery);
        split.Panel2.Controls.Add(preview);
        preview.Dock = DockStyle.Fill;
        page.Controls.Add(split); page.Controls.Add(top); tabs.TabPages.Add(page);
    }

    private void MakeExportTab()
    {
        var page = Page("5 · Export / Boosterplanung");
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(22), AutoScroll = true };
        stack.Controls.Add(L("Exportiere den kompletten Cube als TXT-Liste für Moxfield."));
        stack.Controls.Add(Btn("Moxfield-TXT exportieren", async (_, _) => await ExportTxtAsync()));
        stack.Controls.Add(L("Booster-Kapazität – zunächst nur mathematische Obergrenze ohne Bonusslot und Farbgarantien:"));
        var boundLabel = L(""); boundLabel.Font = new Font(Font, FontStyle.Bold);
        stack.Controls.Add(boundLabel);
        var check = Btn("Obergrenze berechnen", (_, _) => boundLabel.Text = $"Höchstens {CubeLogic.SimpleBoosterBound(project)} Booster nach festen Seltenheits-Slots (nicht als packbar garantiert)");
        stack.Controls.Add(check);
        stack.Controls.Add(L("PDF-Druck, Packlisten, vollständiger Booster-Generator, Draft und Sealed folgen in späteren Meilensteinen."));
        page.Controls.Add(stack); tabs.TabPages.Add(page);
    }

    private async Task WithOperationAsync(string message, Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        operation = new CancellationTokenSource(); cancelButton.Enabled = true;
        status.Text = message;
        try { await action(operation.Token); status.Text = "Fertig"; }
        catch (OperationCanceledException) { status.Text = "Vorgang abgebrochen"; }
        catch (Exception ex) { status.Text = "Fehler"; MessageBox.Show(this, ex.Message, "MTG Cube Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { operation.Dispose(); operation = null; cancelButton.Enabled = false; }
    }

    private async Task LoadSetsAsync()
    {
        await WithOperationAsync("Scryfall-Sets werden geladen ...", async token =>
        {
            var sets = await scryfall.GetSetsAsync(token);
            availableSets.Clear(); availableSets.AddRange(sets);
            setsBox.DataSource = null; setsBox.DataSource = sets;
            setsBox.DisplayMember = nameof(MagicSet.Name);
            var current = availableSets.FindIndex(s => s.Code == project.SetCode);
            if (current >= 0) setsBox.SelectedIndex = current;
        });
    }

    private async Task GenerateSetAsync()
    {
        if (setsBox.SelectedItem is not MagicSet set) { MessageBox.Show("Bitte zunächst ein Set auswählen."); return; }
        if (project.Cards.Count > 0 && MessageBox.Show("Bestehende Karten ersetzen?", "Set-Cube", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await WithOperationAsync("Set-Karten werden geladen ...", async token =>
        {
            var progress = new Progress<int>(n => status.Text = $"{n} Karten eingelesen ...");
            var cards = await scryfall.GetSetCardsAsync(set.Code, project.Language, progress, token);
            var selected = cards.Select(c => { c.Quantity = project.RarityMultipliers.GetValueOrDefault(c.Rarity, 0); return c; }).Where(c => c.Quantity > 0).ToList();
            project.Cards = selected; project.SetCode = set.Code; project.Type = "Set";
            SyncControls(); MarkChanged(); tabs.SelectedIndex = 3;
            MessageBox.Show($"{selected.Count} verschiedene Karten mit insgesamt {CubeLogic.Count(project)} Exemplaren geladen.");
        });
    }

    private async Task SearchAsync()
    {
        string query = searchBox.Text.Trim();
        if (query.Length < 2) { MessageBox.Show("Bitte mindestens zwei Zeichen eingeben."); return; }
        await WithOperationAsync("Karten werden gesucht ...", async token =>
        {
            var cards = await scryfall.SearchCardsAsync(query, token);
            searchResults.DataSource = null; searchResults.DataSource = cards;
            status.Text = $"{cards.Count} Treffer";
        });
    }

    private async Task AddSelectedAsync()
    {
        if (searchResults.SelectedItem is not CubeEntry selected) return;
        await WithOperationAsync("Druckversion wird ermittelt ...", async token =>
        {
        var chosen = await scryfall.LocalizeAsync(Clone(selected), project.Language, token);
        // Kopie anlegen, damit Suchergebnisse und Projekt unabhängig bleiben.
        CubeLogic.Add(project, new CubeEntry
        {
            Id = chosen.Id, OracleId = chosen.OracleId, Name = chosen.Name, PrintedName = chosen.PrintedName, Language = chosen.Language, Set = chosen.Set, CollectorNumber = chosen.CollectorNumber,
            Rarity = chosen.Rarity, TypeLine = chosen.TypeLine, ManaCost = chosen.ManaCost,
            ManaValue = chosen.ManaValue, Colors = chosen.Colors.ToList(), ImageUrl = chosen.ImageUrl
        }, (int)addQuantity.Value);
        MarkChanged();
        });
    }

    private static CubeEntry Clone(CubeEntry c) => new()
    {
        Id = c.Id, OracleId = c.OracleId, Name = c.Name, PrintedName = c.PrintedName, Language = c.Language,
        Set = c.Set, CollectorNumber = c.CollectorNumber, Rarity = c.Rarity, TypeLine = c.TypeLine,
        ManaCost = c.ManaCost, ManaValue = c.ManaValue, Colors = c.Colors.ToList(), ImageUrl = c.ImageUrl, Quantity = c.Quantity
    };

    private async Task RelocalizeAsync()
    {
        await WithOperationAsync("Sprachversionen werden geprüft ...", async token =>
        {
            if (project.Type == "Set" && !string.IsNullOrEmpty(project.SetCode))
            {
                var refreshed = await scryfall.GetSetCardsAsync(project.SetCode, project.Language, null, token);
                var lookup = refreshed.GroupBy(c => c.CollectorNumber).ToDictionary(g => g.Key, g => g.First());
                foreach (var c in project.Cards)
                    if (c.Set == project.SetCode && lookup.TryGetValue(c.CollectorNumber, out var print))
                    { c.Id = print.Id; c.PrintedName = print.PrintedName; c.Language = print.Language; c.ImageUrl = print.ImageUrl; }
            }
            else
            {
                foreach (var c in project.Cards)
                {
                    token.ThrowIfCancellationRequested();
                    if (project.Language == "de") await scryfall.LocalizeAsync(c, "de", token);
                    else
                    {
                        try
                        {
                            // Alte Projekte erhalten zumindest die bestehende englische Originalversion.
                            var english = await scryfall.GetEnglishPrintAsync(c, token);
                            c.Id = english.Id; c.ImageUrl = english.ImageUrl; c.PrintedName = english.PrintedName; c.Language = "en";
                        }
                        catch (InvalidOperationException) { }
                    }
                    await Task.Delay(110, token);
                }
            }
            MarkChanged();
        });
    }

    private CubeEntry? SelectedCard() => grid.Visible ? grid.CurrentRow?.Tag as CubeEntry : null;
    private void AdjustSelected(int delta)
    {
        var card = SelectedCard(); if (card is null) return;
        card.Quantity = Math.Max(0, card.Quantity + delta);
        if (card.Quantity == 0) project.Cards.Remove(card);
        MarkChanged();
    }
    private void RemoveSelected()
    {
        var card = SelectedCard(); if (card is null) return;
        project.Cards.Remove(card); MarkChanged();
    }

    private void RefreshEditor()
    {
        if (grid.Columns.Count == 0) return;
        int mode = Math.Max(0, viewBox.SelectedIndex);
        grid.Visible = mode == 0;
        gallery.Visible = mode != 0;
        grid.Rows.Clear();
        foreach (var c in project.Cards.OrderBy(CubeLogic.Category).ThenBy(CubeLogic.CardType).ThenBy(c => c.Name))
        {
            int row = grid.Rows.Add(c.Quantity, CubeLogic.DisplayName(c), CubeLogic.Category(c), CubeLogic.CardType(c), c.Rarity, c.Set.ToUpperInvariant(), c.ManaCost);
            grid.Rows[row].Tag = c;
        }
        gallery.ShowCube(project, mode == 1);
        summary.Text = $"   {project.Name} | {CubeLogic.Count(project)} Karten / {project.Cards.Count} verschiedene | {project.Type}" + (dirty ? " *" : "");
    }

    private async Task ShowImageAsync(CubeEntry card, PictureBox? target = null)
    {
        var box = target ?? preview;
        if (string.IsNullOrWhiteSpace(card.ImageUrl)) return;
        if (images.TryGetValue(card.ImageUrl, out var saved)) { if (!box.IsDisposed) box.Image = saved; return; }
        try
        {
            byte[] content = await imageClient.GetByteArrayAsync(card.ImageUrl);
            using var ms = new MemoryStream(content);
            using var original = Image.FromStream(ms);
            var bitmap = new Bitmap(original);
            images[card.ImageUrl] = bitmap;
            if (!box.IsDisposed && !IsDisposed) box.Image = bitmap;
        }
        catch { /* Bildfehler dürfen die Kartenbearbeitung nicht blockieren. */ }
    }

    private void SyncControls()
    {
        syncing = true;
        nameBox.Text = project.Name;
        typeBox.SelectedItem = project.Type;
        languageBox.SelectedIndex = project.Language == "de" ? 1 : 0;
        targetBox.Value = Math.Clamp(project.TargetSize, (int)targetBox.Minimum, (int)targetBox.Maximum);
        foreach (var (rarity, control) in rarityInputs)
            control.Value = Math.Clamp(project.RarityMultipliers.GetValueOrDefault(rarity), (int)control.Minimum, (int)control.Maximum);
        syncing = false;
        if (viewBox.SelectedIndex < 0) viewBox.SelectedIndex = 2;
        RefreshEditor();
    }

    private void MarkChanged() { dirty = true; RefreshEditor(); }
    private async Task NewProjectAsync()
    {
        if (!ConfirmDiscard()) return;
        project = new CubeProject(); projectPath = null; dirty = false; SyncControls();
        await Task.CompletedTask;
    }
    private bool ConfirmDiscard() => !dirty || MessageBox.Show("Nicht gespeicherte Änderungen verwerfen?", "Projekt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private async Task OpenProjectAsync()
    {
        if (!ConfirmDiscard()) return;
        using var dialog = new OpenFileDialog { Filter = "Cube-Projekt (*.cube.json)|*.cube.json|JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { project = await ProjectService.LoadAsync(dialog.FileName); projectPath = dialog.FileName; dirty = false; SyncControls(); }
        catch (Exception ex) { MessageBox.Show($"Projekt konnte nicht geöffnet werden: {ex.Message}"); }
    }
    private async Task SaveProjectAsync(bool saveAs = false)
    {
        string? path = saveAs ? null : projectPath;
        if (path is null)
        {
            using var dialog = new SaveFileDialog { Filter = "Cube-Projekt (*.cube.json)|*.cube.json", FileName = project.Name + ".cube.json" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            path = dialog.FileName;
        }
        try { await ProjectService.SaveAsync(project, path); projectPath = path; dirty = false; RefreshEditor(); status.Text = "Projekt gespeichert"; }
        catch (Exception ex) { MessageBox.Show($"Speichern fehlgeschlagen: {ex.Message}"); }
    }
    private async Task ExportTxtAsync()
    {
        using var dialog = new SaveFileDialog { Filter = "Textliste (*.txt)|*.txt", FileName = project.Name + ".txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await ProjectService.ExportTxtAsync(project, dialog.FileName); status.Text = "Textliste exportiert"; }
        catch (Exception ex) { MessageBox.Show($"Export fehlgeschlagen: {ex.Message}"); }
    }
    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmDiscard()) { e.Cancel = true; return; }
        operation?.Cancel();
        foreach (var img in images.Values) img.Dispose();
        imageClient.Dispose(); scryfall.Dispose();
    }
}
