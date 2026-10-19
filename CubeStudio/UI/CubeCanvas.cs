using System.Drawing.Drawing2D;
using CubeStudio.Models;
using CubeStudio.Services;

namespace CubeStudio.UI;

/// <summary>
/// Zeichnet die Cube-Spalten selbst. Im Gegensatz zu hunderten PictureBoxen bleiben
/// Scrollen, Hover und Mengenbearbeitung auch bei großen Cubes beherrschbar.
/// </summary>
public sealed class CubeCanvas : ScrollableControl
{
    private static readonly string[] Categories = { "Weiß", "Blau", "Schwarz", "Rot", "Grün", "Mehrfarbig", "Farblos", "Länder", "Token" };
    private static readonly string[] Types = { "Kreaturen", "Spontanzauber", "Hexereien", "Verzauberungen", "Artefakte", "Sonstiges" };
    private readonly List<(Rectangle Area, CubeEntry Card)> hitAreas = new();
    private readonly Dictionary<string, Image> thumbnails = new();
    private readonly HashSet<string> loading = new();
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SemaphoreSlim imageSlots = new(3);
    private CubeProject project = new();
    private CubeEntry? hovered;
    private bool gridMode;
    private bool disposed;
    private const int ColumnWidth = 218;
    private const int ColumnGap = 12;
    private const int StackHeight = 42;
    private const int GridHeight = 280;
    public event Action<CubeEntry>? CardHovered;
    public event Action<CubeEntry>? QuantityChanged;

    public CubeCanvas()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        BackColor = Color.FromArgb(29, 32, 38);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9f);
    }

    public void ShowCube(CubeProject source, bool grid)
    {
        project = source;
        gridMode = grid;
        AutoScrollMinSize = new Size(Categories.Length * (ColumnWidth + ColumnGap) + 15, ComputeHeight());
        Invalidate();
    }

    private int ComputeHeight()
    {
        int height = 500;
        foreach (var category in Categories)
        {
            var cards = project.Cards.Where(c => c.Quantity > 0 && CubeLogic.Category(c) == category).ToList();
            int total = 58;
            foreach (var type in Types)
            {
                int count = cards.Count(c => CubeLogic.CardType(c) == type);
                if (count > 0) total += 34 + count * (gridMode ? GridHeight : StackHeight) + 12;
            }
            height = Math.Max(height, total + 30);
        }
        return height;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        hitAreas.Clear();
        using var darkBrush = new SolidBrush(Color.FromArgb(40, 44, 51));
        using var headerBrush = new SolidBrush(Color.FromArgb(225, 231, 236));
        using var mutedBrush = new SolidBrush(Color.FromArgb(173, 181, 191));
        using var typeBrush = new SolidBrush(Color.FromArgb(65, 70, 80));
        for (int ci = 0; ci < Categories.Length; ci++)
        {
            var category = Categories[ci];
            int x = 14 + ci * (ColumnWidth + ColumnGap);
            var cards = project.Cards.Where(c => c.Quantity > 0 && CubeLogic.Category(c) == category).ToList();
            e.Graphics.FillRectangle(darkBrush, x, 12, ColumnWidth, 39);
            e.Graphics.DrawString($"{category} ({cards.Sum(c => c.Quantity)})", Font, headerBrush, x + 8, 22);
            int y = 62;
            foreach (string type in Types)
            {
                var group = cards.Where(c => CubeLogic.CardType(c) == type)
                    .OrderBy(c => c.ManaValue).ThenBy(c => CubeLogic.DisplayName(c)).ToList();
                if (group.Count == 0) continue;
                e.Graphics.FillRectangle(typeBrush, x, y, ColumnWidth, 26);
                e.Graphics.DrawString($"{type} ({group.Sum(c => c.Quantity)})", Font, headerBrush, x + 6, y + 4);
                y += 31;
                foreach (var card in group)
                {
                    int h = gridMode ? GridHeight - 4 : StackHeight - 3;
                    var bounds = new Rectangle(x + 4, y, ColumnWidth - 8, h);
                    bool hot = ReferenceEquals(card, hovered);
                    var baseColor = CategoryColor(category);
                    using (var fill = new SolidBrush(hot ? ControlPaint.Light(baseColor, 0.20f) : baseColor))
                        e.Graphics.FillRectangle(fill, bounds);
                    if (!string.IsNullOrEmpty(card.ImageUrl))
                    {
                        string url = SmallImageUrl(card.ImageUrl);
                        if (thumbnails.TryGetValue(url, out var image))
                        {
                            if (gridMode) e.Graphics.DrawImage(image, bounds);
                            else
                            {
                                // Obere Kartenkante einschließlich Name/Mana; kein verzerrtes Kartenbild.
                                var crop = new Rectangle(0, 0, image.Width, Math.Max(1, (int)(image.Width * h / (double)bounds.Width)));
                                crop.Height = Math.Min(crop.Height, image.Height);
                                e.Graphics.DrawImage(image, bounds, crop, GraphicsUnit.Pixel);
                            }
                        }
                        else if (bounds.Right + AutoScrollPosition.X >= 0 && bounds.Left + AutoScrollPosition.X < ClientSize.Width &&
                                 bounds.Bottom + AutoScrollPosition.Y >= 0 && bounds.Top + AutoScrollPosition.Y < ClientSize.Height)
                            QueueImage(url);
                    }
                    if (!thumbnails.ContainsKey(SmallImageUrl(card.ImageUrl)))
                        TextRenderer.DrawText(e.Graphics, CubeLogic.DisplayName(card), Font,
                            new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 30, 26), Color.FromArgb(20, 20, 20),
                            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    using (var badge = new SolidBrush(Color.FromArgb(11, 12, 15)))
                        e.Graphics.FillEllipse(badge, bounds.X - 4, bounds.Y - 3, 27, 24);
                    TextRenderer.DrawText(e.Graphics, card.Quantity.ToString(), Font,
                        new Rectangle(bounds.X - 3, bounds.Y, 25, 18), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    if (hot) using (var pen = new Pen(Color.Gold, 2)) e.Graphics.DrawRectangle(pen, bounds);
                    hitAreas.Add((bounds, card));
                    y += gridMode ? GridHeight : StackHeight;
                }
                y += 12;
            }
        }
    }

    private static Color CategoryColor(string category) => category switch
    {
        "Weiß" => Color.FromArgb(228, 218, 186), "Blau" => Color.FromArgb(110, 167, 212),
        "Schwarz" => Color.FromArgb(139, 133, 151), "Rot" => Color.FromArgb(208, 114, 95),
        "Grün" => Color.FromArgb(109, 166, 114), "Mehrfarbig" => Color.FromArgb(210, 178, 106),
        _ => Color.FromArgb(171, 168, 161)
    };

    private static string SmallImageUrl(string url) => url.Replace("/normal/", "/small/");

    private async void QueueImage(string url)
    {
        if (!loading.Add(url)) return;
        try
        {
            await imageSlots.WaitAsync();
            try
            {
                if (disposed) return;
                byte[] bytes = await client.GetByteArrayAsync(url);
                using var stream = new MemoryStream(bytes);
                using var original = Image.FromStream(stream);
                var bitmap = new Bitmap(original);
                if (disposed) { bitmap.Dispose(); return; }
                thumbnails[url] = bitmap;
                Invalidate();
                await Task.Delay(110);
            }
            finally { imageSlots.Release(); }
        }
        catch { /* Ein fehlendes Bild darf die übrige Oberfläche nicht blockieren. */ }
        finally { loading.Remove(url); }
    }

    private CubeEntry? At(Point point)
    {
        var p = new Point(point.X - AutoScrollPosition.X, point.Y - AutoScrollPosition.Y);
        for (int i = hitAreas.Count - 1; i >= 0; i--)
            if (hitAreas[i].Area.Contains(p)) return hitAreas[i].Card;
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var next = At(e.Location);
        if (ReferenceEquals(next, hovered)) return;
        hovered = next;
        Cursor = hovered is null ? Cursors.Default : Cursors.Hand;
        if (hovered is not null) CardHovered?.Invoke(hovered);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var card = At(e.Location);
        if (card is null || e.Button != MouseButtons.Left) return;
        ShowQuantityOverlay(card, e.Location);
    }

    private void ShowQuantityOverlay(CubeEntry card, Point location)
    {
        var popup = new ToolStripDropDown { AutoClose = true, Padding = new Padding(5) };
        var content = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Padding = new Padding(6), BackColor = Color.WhiteSmoke };
        var minus = new Button { Text = "−", Width = 40, Height = 32 };
        var count = new Label { Text = card.Quantity.ToString(), AutoSize = false, Width = 42, Height = 32, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Black };
        var plus = new Button { Text = "+", Width = 40, Height = 32 };
        void Update(int delta)
        {
            card.Quantity = Math.Max(0, card.Quantity + delta);
            count.Text = card.Quantity.ToString();
            if (card.Quantity == 0) { project.Cards.Remove(card); popup.Close(); }
            ShowCube(project, gridMode);
            QuantityChanged?.Invoke(card);
        }
        minus.Click += (_, _) => Update(-1);
        plus.Click += (_, _) => Update(1);
        content.Controls.AddRange(new Control[] { minus, count, plus });
        popup.Items.Add(new ToolStripControlHost(content) { Margin = Padding.Empty, Padding = Padding.Empty });
        popup.Show(this, location.X, location.Y + 8);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposed = true;
            client.Dispose();
            foreach (var image in thumbnails.Values) image.Dispose();
            // Semaphore wird bewusst nicht entsorgt, solange Bild-Tasks auslaufen können.
        }
        base.Dispose(disposing);
    }
}
