using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PraatjeNL.Capture;
using PraatjeNL.Text;
using StardewValley;

namespace PraatjeNL.UI
{
    /// <summary>What the reader needs to know about a word to draw and describe it.</summary>
    internal sealed class WordInfo
    {
        public string Word = "";
        public string Lemma = "";
        public string Meaning = "";
        public bool IsName;
        public bool IsSaved;
        public bool Searching;
    }

    /// <summary>
    /// The reader: a movable panel with the Dutch text on screen, every word clickable,
    /// and the original English next to it (toggle). Like Speurtocht.NL's reader.
    /// </summary>
    internal sealed class ReaderPanel
    {
        private const int Pad = 20;
        private const int HeaderH = 48;
        private const int ColGap = 24;

        private readonly ModEntry mod;
        private CapturedText? text;
        private string identity = "";
        private List<PlacedToken> nlTokens = new();
        private List<PlacedToken> enTokens = new();
        private float nlHeight;
        private float enHeight;
        private float scroll;
        private int layoutWidth = -1;
        private bool layoutEnglish;
        private Rectangle bounds;
        private Rectangle textArea;
        private Rectangle enButton, listButton, closeButton;
        private bool dragging;
        private Point dragOffset;
        private PlacedToken? hovered;

        public ReaderPanel(ModEntry mod)
        {
            this.mod = mod;
        }

        public bool HasText => this.text != null;
        public Rectangle Bounds => this.bounds;

        private float Scale => this.mod.Config.FontScale;
        private int ColumnWidth => this.mod.Config.ReaderColumnWidth;

        public void SetText(CapturedText? captured)
        {
            this.text = captured;
            string id = captured?.Identity ?? "";
            if (id != this.identity)
            {
                this.identity = id;
                this.scroll = 0;
                this.layoutWidth = -1;
                this.hovered = null;
            }
        }

        /// <summary>Re-colour words (saved / name) on the next draw.</summary>
        public void Invalidate() => this.layoutWidth = -1;

        private void EnsureLayout()
        {
            bool showEn = this.mod.Config.ShowEnglish && this.text?.English != null;
            if (this.layoutWidth == this.ColumnWidth && this.layoutEnglish == showEn)
                return;
            this.layoutWidth = this.ColumnWidth;
            this.layoutEnglish = showEn;
            SpriteFont font = Game1.smallFont;
            float lineH = font.LineSpacing * this.Scale;
            Func<string, float> measure = s => Ui.Measure(font, s, this.Scale);
            this.nlTokens = this.text == null
                ? new List<PlacedToken>()
                : TextLayout.Layout(this.text.Blocks, this.ColumnWidth, lineH, lineH * 0.6f, measure, out this.nlHeight);
            if (showEn)
                this.enTokens = TextLayout.Layout(new[] { this.text!.English! }, this.ColumnWidth, lineH, 0, measure, out this.enHeight);
            else
            {
                this.enTokens = new List<PlacedToken>();
                this.enHeight = 0;
            }
        }

        private void PlaceBounds()
        {
            int screenW = Game1.uiViewport.Width;
            int screenH = Game1.uiViewport.Height;
            bool showEn = this.layoutEnglish;
            int w = this.ColumnWidth * (showEn ? 2 : 1) + (showEn ? ColGap : 0) + Pad * 2;
            float content = Math.Max(this.nlHeight, this.enHeight);
            int maxH = (int)(screenH * this.mod.Config.ReaderMaxHeight);
            int h = (int)Math.Min(maxH, HeaderH + content + Pad * 2);
            h = Math.Max(h, HeaderH + Pad * 2 + 40);

            int x = this.mod.Config.ReaderX;
            int y = this.mod.Config.ReaderY;
            if (x < 0 || y < 0)
            {
                // Default: top-left, clear of the dialogue box at the bottom
                x = 32;
                y = 32;
            }
            x = Math.Clamp(x, 0, Math.Max(0, screenW - w));
            y = Math.Clamp(y, 0, Math.Max(0, screenH - h));
            this.bounds = new Rectangle(x, y, w, h);
            this.textArea = new Rectangle(x + Pad, y + HeaderH + Pad / 2, w - Pad * 2, h - HeaderH - Pad);
        }

        private float MaxScroll => Math.Max(0, Math.Max(this.nlHeight, this.enHeight) - this.textArea.Height);

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------
        public void Draw(SpriteBatch b)
        {
            if (this.text == null)
                return;
            this.EnsureLayout();
            this.PlaceBounds();
            this.scroll = Math.Clamp(this.scroll, 0, this.MaxScroll);

            SpriteFont font = Game1.smallFont;
            int mx = Game1.getMouseX(true), my = Game1.getMouseY(true);
            Ui.Box(b, this.bounds);

            // Header: source + buttons
            string title = this.text.Source.Length > 0 ? this.text.Source : "Praatje.NL";
            int bx = this.bounds.Right - Pad;
            int by = this.bounds.Y + 14;
            string close = "x";
            string list = $"Woorden ({this.mod.Vocab.Entries.Count})";
            string en = this.mod.Config.ShowEnglish ? "- EN" : "+ EN";
            bx -= Ui.ButtonWidth(font, close, 0.8f);
            this.closeButton = Ui.Button(b, font, close, bx, by, this.closeButton.Contains(mx, my), 0.8f);
            bx -= Ui.ButtonWidth(font, list, 0.8f) + 6;
            this.listButton = Ui.Button(b, font, list, bx, by, this.listButton.Contains(mx, my), 0.8f);
            bx -= Ui.ButtonWidth(font, en, 0.8f) + 6;
            this.enButton = Ui.Button(b, font, en, bx, by, this.enButton.Contains(mx, my), 0.8f);
            Ui.Text(b, font, title, this.bounds.X + Pad + 4, this.bounds.Y + 18, Game1.textColor, this.Scale);
            Ui.Line(b, this.bounds.X + Pad, this.bounds.Y + HeaderH, this.bounds.Width - Pad * 2, Ui.Muted * 0.5f);

            // Dutch column
            this.hovered = null;
            float x0 = this.textArea.X, y0 = this.textArea.Y - this.scroll;
            foreach (PlacedToken t in this.nlTokens)
            {
                if (!this.Visible(t, y0))
                    continue;
                Color color = Game1.textColor;
                if (t.Token.IsWord)
                {
                    WordInfo info = this.mod.Describe(t.Token.Text, lookupOnline: false);
                    if (info.IsName)
                        color = Ui.Name;
                    else if (info.IsSaved)
                        color = Ui.Saved;
                    if (t.Contains(mx - x0, my - y0) && this.textArea.Contains(mx, my))
                    {
                        this.hovered = t;
                        Ui.Line(b, (int)(x0 + t.X), (int)(y0 + t.Y + t.Height - 4), (int)t.Width, Ui.Hover);
                        color = info.IsName ? Ui.Name : Ui.Hover;
                    }
                }
                Ui.Text(b, font, t.Token.Text, x0 + t.X, y0 + t.Y, color, this.Scale);
            }

            // English column
            if (this.layoutEnglish)
            {
                float ex = x0 + this.ColumnWidth + ColGap;
                b.Draw(Game1.staminaRect, new Rectangle((int)(ex - ColGap / 2), this.textArea.Y, 2, this.textArea.Height), Ui.Muted * 0.3f);
                foreach (PlacedToken t in this.enTokens)
                {
                    if (this.Visible(t, y0))
                        Ui.Text(b, font, t.Token.Text, ex + t.X, y0 + t.Y, Ui.English, this.Scale * 0.95f);
                }
            }
            else if (this.mod.Config.ShowEnglish && this.text.English == null)
            {
                // nothing found: say so once, quietly, under the header
                Ui.Text(b, font, "(geen Engelse tekst gevonden)", this.bounds.Right - Pad - Ui.Measure(font, "(geen Engelse tekst gevonden)", 0.7f), this.bounds.Y + HeaderH + 2, Ui.Muted, 0.7f);
            }

            // Scroll hint
            if (this.MaxScroll > 0)
            {
                float frac = this.scroll / this.MaxScroll;
                int barH = Math.Max(20, (int)(this.textArea.Height * this.textArea.Height / Math.Max(this.nlHeight, this.enHeight)));
                int barY = this.textArea.Y + (int)((this.textArea.Height - barH) * frac);
                b.Draw(Game1.staminaRect, new Rectangle(this.bounds.Right - 14, barY, 4, barH), Ui.Muted * 0.6f);
            }
        }

        public void DrawTooltip(SpriteBatch b)
        {
            if (this.hovered == null || !this.hovered.Token.IsWord)
                return;
            WordInfo info = this.mod.Describe(this.hovered.Token.Text, lookupOnline: true);
            string body;
            if (info.IsName)
                body = "Naam - wordt niet opgeslagen.\n(Name - not saved.)";
            else
            {
                var lines = new List<string>();
                if (info.Lemma.Length > 0 && info.Lemma != WordSplitter.Normalize(info.Word))
                    lines.Add("van: " + info.Lemma);
                lines.Add(info.Meaning.Length > 0 ? info.Meaning
                    : info.Searching ? "Zoeken op Wiktionary..."
                    : "(nog geen betekenis - je kunt hem in Anki invullen)");
                lines.Add(info.IsSaved ? "Opgeslagen - rechtsklik = verwijderen" : "Klik = opslaan");
                body = string.Join("\n", lines);
            }
            Ui.Tooltip(b, this.hovered.Token.Text, Wrap(body, 360));
        }

        private bool Visible(PlacedToken t, float y0)
        {
            float top = y0 + t.Y;
            return top >= this.textArea.Y - 2 && top + t.Height <= this.textArea.Bottom + 2;
        }

        private static string Wrap(string text, int width)
        {
            var lines = new List<string>();
            foreach (string para in text.Split('\n'))
                lines.Add(Game1.parseText(para, Game1.smallFont, width));
            return string.Join("\n", lines);
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------

        /// <summary>Handle a click. Returns true when the click was on the panel (and must not reach the game).</summary>
        public bool Click(int x, int y, bool right)
        {
            if (this.text == null || !this.bounds.Contains(x, y))
                return false;
            if (!right)
            {
                if (this.closeButton.Contains(x, y)) { this.mod.HideReader(); return true; }
                if (this.listButton.Contains(x, y)) { this.mod.ToggleWordList(); return true; }
                if (this.enButton.Contains(x, y))
                {
                    this.mod.Config.ShowEnglish = !this.mod.Config.ShowEnglish;
                    this.mod.SaveConfig();
                    this.Invalidate();
                    return true;
                }
                if (y < this.bounds.Y + HeaderH)
                {
                    this.dragging = true;
                    this.dragOffset = new Point(x - this.bounds.X, y - this.bounds.Y);
                    return true;
                }
            }
            float x0 = this.textArea.X, y0 = this.textArea.Y - this.scroll;
            if (this.textArea.Contains(x, y))
            {
                foreach (PlacedToken t in this.nlTokens)
                {
                    if (t.Token.IsWord && t.Contains(x - x0, y - y0))
                    {
                        string block = this.text.Blocks[t.Block];
                        string sentence = WordSplitter.SentenceAround(block, t.Token.Start);
                        this.mod.OnWordClicked(t.Token.Text, sentence, this.text.Source, right);
                        return true;
                    }
                }
            }
            return true;
        }

        public void Update(int x, int y, bool leftDown)
        {
            if (!this.dragging)
                return;
            if (!leftDown)
            {
                this.dragging = false;
                this.mod.SaveConfig();
                return;
            }
            this.mod.Config.ReaderX = Math.Max(0, x - this.dragOffset.X);
            this.mod.Config.ReaderY = Math.Max(0, y - this.dragOffset.Y);
        }

        public bool Scroll(int x, int y, int delta)
        {
            if (this.text == null || !this.bounds.Contains(x, y))
                return false;
            this.scroll = Math.Clamp(this.scroll - delta / 2f, 0, this.MaxScroll);
            return true;
        }
    }
}
