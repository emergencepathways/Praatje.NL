using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PraatjeNL.Vocab;
using StardewValley;

namespace PraatjeNL.UI
{
    /// <summary>"Mijn woorden": the saved word list, with Anki and export buttons.</summary>
    internal sealed class WordListPanel
    {
        private const int Pad = 20;
        private const int HeaderH = 52;
        private const int FooterH = 56;
        private const int Width = 560;

        private readonly ModEntry mod;
        private Rectangle bounds;
        private Rectangle ankiButton, exportButton, copyButton, closeButton;
        private float scroll;
        private VocabEntry? hovered;
        private readonly List<(Rectangle area, VocabEntry entry)> rows = new();

        public WordListPanel(ModEntry mod)
        {
            this.mod = mod;
        }

        public bool Open { get; set; }
        public Rectangle Bounds => this.bounds;

        private float RowH => Game1.smallFont.LineSpacing * 0.9f + 6;

        private void Place()
        {
            int screenW = Game1.uiViewport.Width;
            int screenH = Game1.uiViewport.Height;
            int h = Math.Min(screenH - 64, 640);
            Rectangle reader = this.mod.Reader.HasText ? this.mod.Reader.Bounds : Rectangle.Empty;
            int x = reader != Rectangle.Empty && reader.Right + 12 + Width <= screenW ? reader.Right + 12 : screenW - Width - 32;
            int y = reader != Rectangle.Empty ? reader.Y : 32;
            this.bounds = new Rectangle(Math.Max(0, x), Math.Clamp(y, 0, Math.Max(0, screenH - h)), Width, h);
        }

        public void Draw(SpriteBatch b)
        {
            if (!this.Open)
                return;
            this.Place();
            SpriteFont font = Game1.smallFont;
            int mx = Game1.getMouseX(true), my = Game1.getMouseY(true);
            Ui.Box(b, this.bounds);

            List<VocabEntry> list = this.mod.Vocab.Sorted();
            int waiting = this.mod.AnkiWaiting;
            Ui.Text(b, font, $"Mijn woorden - {list.Count}", this.bounds.X + Pad + 4, this.bounds.Y + 18, Game1.textColor, 1f);
            this.closeButton = Ui.Button(b, font, "x", this.bounds.Right - Pad - Ui.ButtonWidth(font, "x", 0.8f), this.bounds.Y + 14, this.closeButton.Contains(mx, my), 0.8f);
            Ui.Line(b, this.bounds.X + Pad, this.bounds.Y + HeaderH, this.bounds.Width - Pad * 2, Ui.Muted * 0.5f);

            // Rows
            var area = new Rectangle(this.bounds.X + Pad, this.bounds.Y + HeaderH + 6, this.bounds.Width - Pad * 2, this.bounds.Height - HeaderH - FooterH - 6);
            float total = list.Count * this.RowH;
            this.scroll = Math.Clamp(this.scroll, 0, Math.Max(0, total - area.Height));
            this.rows.Clear();
            this.hovered = null;
            for (int i = 0; i < list.Count; i++)
            {
                float y = area.Y + i * this.RowH - this.scroll;
                if (y < area.Y - 1 || y + this.RowH > area.Bottom + 1)
                    continue;
                VocabEntry e = list[i];
                var row = new Rectangle(area.X, (int)y, area.Width, (int)this.RowH);
                this.rows.Add((row, e));
                if (row.Contains(mx, my))
                {
                    this.hovered = e;
                    b.Draw(Game1.staminaRect, row, Color.Wheat * 0.5f);
                }
                Ui.Text(b, font, Fit(font, e.Word, 170, 0.9f), row.X + 4, row.Y + 3, e.SentToAnki != null ? Ui.Saved : Game1.textColor, 0.9f);
                string meaning = e.Meaning.Length > 0 ? e.Meaning : "-";
                Ui.Text(b, font, Fit(font, meaning, area.Width - 190, 0.85f), row.X + 184, row.Y + 4, Ui.English, 0.85f);
            }
            if (list.Count == 0)
                Ui.Text(b, font, "Nog geen woorden. Klik op een woord in de lezer.", area.X + 4, area.Y + 6, Ui.Muted, 0.85f);

            // Footer buttons
            int fy = this.bounds.Bottom - FooterH + 10;
            int fx = this.bounds.X + Pad;
            string ankiLabel = this.mod.Config.AnkiEnabled ? (waiting > 0 ? $"Naar Anki ({waiting})" : "Naar Anki") : "Anki uit";
            this.ankiButton = Ui.Button(b, font, ankiLabel, fx, fy, this.ankiButton.Contains(mx, my), 0.85f);
            fx = this.ankiButton.Right + 8;
            this.exportButton = Ui.Button(b, font, "Exporteer .txt", fx, fy, this.exportButton.Contains(mx, my), 0.85f);
            fx = this.exportButton.Right + 8;
            this.copyButton = Ui.Button(b, font, "Kopieer", fx, fy, this.copyButton.Contains(mx, my), 0.85f);
        }

        public void DrawTooltip(SpriteBatch b)
        {
            if (!this.Open)
                return;
            int mx = Game1.getMouseX(true), my = Game1.getMouseY(true);
            if (this.hovered != null)
            {
                VocabEntry e = this.hovered;
                var lines = new List<string>();
                if (e.Lemma.Length > 0 && e.Lemma != e.Word)
                    lines.Add("van: " + e.Lemma);
                lines.Add(e.Meaning.Length > 0 ? e.Meaning : "(nog geen betekenis)");
                if (e.Sentence.Length > 0)
                    lines.Add("\"" + e.Sentence + "\"");
                if (e.Source.Length > 0)
                    lines.Add(e.Source);
                lines.Add(e.SentToAnki != null ? "In Anki" : "Nog niet in Anki");
                lines.Add("Rechtsklik = verwijderen");
                var wrapped = new List<string>();
                foreach (string l in lines)
                    wrapped.Add(Game1.parseText(l, Game1.smallFont, 380));
                Ui.Tooltip(b, e.Word, string.Join("\n", wrapped));
            }
            else if (this.ankiButton.Contains(mx, my))
                Ui.Tooltip(b, "Naar Anki", Game1.parseText("Sends words that aren't in Anki yet. Anki must be open with the AnkiConnect add-on (code 2055492159). New words are also sent automatically when you save them.", Game1.smallFont, 380));
            else if (this.exportButton.Contains(mx, my))
                Ui.Tooltip(b, "Exporteer .txt", Game1.parseText("Writes every word to an Anki-ready text file in Documents/PraatjeNL (File > Import in Anki), and opens nothing else.", Game1.smallFont, 380));
            else if (this.copyButton.Contains(mx, my))
                Ui.Tooltip(b, "Kopieer", Game1.parseText("Copies the same text to the clipboard, to paste into a document or Anki.", Game1.smallFont, 380));
        }

        public bool Click(int x, int y, bool right)
        {
            if (!this.Open || !this.bounds.Contains(x, y))
                return false;
            if (!right)
            {
                if (this.closeButton.Contains(x, y)) this.Open = false;
                else if (this.ankiButton.Contains(x, y)) this.mod.SendAllToAnki(manual: true);
                else if (this.exportButton.Contains(x, y)) this.mod.ExportText(toClipboard: false);
                else if (this.copyButton.Contains(x, y)) this.mod.ExportText(toClipboard: true);
                return true;
            }
            foreach ((Rectangle area, VocabEntry entry) in this.rows)
            {
                if (area.Contains(x, y))
                {
                    this.mod.RemoveWord(entry.Word);
                    break;
                }
            }
            return true;
        }

        public bool Scroll(int x, int y, int delta)
        {
            if (!this.Open || !this.bounds.Contains(x, y))
                return false;
            this.scroll = Math.Max(0, this.scroll - delta / 2f);
            return true;
        }

        private static string Fit(SpriteFont font, string text, int width, float scale)
        {
            if (Ui.Measure(font, text, scale) <= width)
                return text;
            while (text.Length > 1 && Ui.Measure(font, text + "...", scale) > width)
                text = text.Substring(0, text.Length - 1);
            return text + "...";
        }
    }
}
