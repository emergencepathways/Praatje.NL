using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PraatjeNL.UI
{
    /// <summary>Small drawing helpers shared by the panels.</summary>
    internal static class Ui
    {
        public static readonly Color Saved = new(28, 120, 36);
        public static readonly Color Name = new(176, 36, 30);
        public static readonly Color Hover = new(30, 80, 170);
        public static readonly Color Muted = new(110, 90, 70);
        public static readonly Color English = new(95, 80, 65);

        private static SpriteFont? checkedFont;
        private static HashSet<char>? fontChars;

        /// <summary>Replace characters the font can't draw (it would throw) with '?'.</summary>
        public static string Safe(SpriteFont font, string text)
        {
            if (!ReferenceEquals(font, checkedFont))
            {
                checkedFont = font;
                fontChars = new HashSet<char>(font.Characters);
            }
            if (font.DefaultCharacter.HasValue)
                return text;
            StringBuilder? sb = null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || fontChars!.Contains(c))
                    continue;
                sb ??= new StringBuilder(text);
                sb[i] = c switch { '’' or '‘' => '\'', '“' or '”' or '„' => '"', '…' => '.', '–' or '—' => '-', _ => '?' };
            }
            return sb?.ToString() ?? text;
        }

        public static float Measure(SpriteFont font, string text, float scale) => font.MeasureString(Safe(font, text)).X * scale;

        public static void Text(SpriteBatch b, SpriteFont font, string text, float x, float y, Color color, float scale)
        {
            b.DrawString(font, Safe(font, text), new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        /// <summary>Parchment box like the game's menus.</summary>
        public static void Box(SpriteBatch b, Rectangle r)
        {
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), r.X, r.Y, r.Width, r.Height, Color.White, 1f, drawShadow: true);
        }

        /// <summary>A small text button. Returns its area.</summary>
        public static Rectangle Button(SpriteBatch b, SpriteFont font, string label, int x, int y, bool hovered, float scale)
        {
            int w = (int)(Measure(font, label, scale) + 20);
            int h = (int)(font.LineSpacing * scale + 8);
            var r = new Rectangle(x, y, w, h);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), r.X, r.Y, r.Width, r.Height, hovered ? Color.Wheat : Color.White, 4f * 0.5f, drawShadow: false);
            Text(b, font, label, x + 10, y + 4, Game1.textColor, scale);
            return r;
        }

        public static int ButtonWidth(SpriteFont font, string label, float scale) => (int)(Measure(font, label, scale) + 20);

        public static void Line(SpriteBatch b, int x, int y, int width, Color color, int thickness = 2)
        {
            b.Draw(Game1.staminaRect, new Rectangle(x, y, width, thickness), color);
        }

        public static void Tooltip(SpriteBatch b, string title, string body)
        {
            IClickableMenu.drawHoverText(b, Safe(Game1.smallFont, body), Game1.smallFont, 0, 0, -1, Safe(Game1.dialogueFont, title));
        }
    }
}
