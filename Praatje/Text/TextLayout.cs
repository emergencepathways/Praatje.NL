using System;
using System.Collections.Generic;

namespace PraatjeNL.Text
{
    /// <summary>A token placed on screen, relative to the top-left of the text area.</summary>
    public sealed class PlacedToken
    {
        public PlacedToken(Token token, int block, float x, float y, float width, float height)
        {
            this.Token = token;
            this.Block = block;
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
        }

        public Token Token { get; }

        /// <summary>Index of the text block this token belongs to (Dutch text, a response, ...).</summary>
        public int Block { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public bool Contains(float px, float py) => px >= this.X && px < this.X + this.Width && py >= this.Y && py < this.Y + this.Height;
    }

    /// <summary>Word-wraps text into lines. Pure logic: the caller supplies the font measurement.</summary>
    public static class TextLayout
    {
        /// <summary>Lay out several text blocks below each other, with <paramref name="blockGap"/> between them.</summary>
        public static List<PlacedToken> Layout(IReadOnlyList<string> blocks, float maxWidth, float lineHeight, float blockGap, Func<string, float> measure, out float totalHeight)
        {
            var placed = new List<PlacedToken>();
            float y = 0;
            for (int b = 0; b < blocks.Count; b++)
            {
                if (b > 0)
                    y += blockGap;
                y = LayoutBlock(blocks[b], b, maxWidth, lineHeight, measure, y, placed);
            }
            totalHeight = y;
            return placed;
        }

        private static float LayoutBlock(string text, int block, float maxWidth, float lineHeight, Func<string, float> measure, float y, List<PlacedToken> placed)
        {
            float x = 0;
            bool lineHasContent = false;
            foreach (Token token in WordSplitter.Split(text))
            {
                if (!token.IsWord)
                {
                    // Separators may contain line breaks: place each piece, break the line at '\n'.
                    string[] parts = token.Text.Split('\n');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (i > 0)
                        {
                            y += lineHeight;
                            x = 0;
                            lineHasContent = false;
                        }
                        string part = parts[i];
                        if (part.Length == 0)
                            continue;
                        if (!lineHasContent)
                            part = part.TrimStart();
                        if (part.Length == 0)
                            continue;
                        float w = measure(part);
                        if (x + w > maxWidth && lineHasContent && part.Trim().Length == 0)
                            continue; // don't start a new line with spaces
                        placed.Add(new PlacedToken(new Token(part, false, token.Start), block, x, y, w, lineHeight));
                        x += w;
                        lineHasContent = true;
                    }
                    continue;
                }

                float width = measure(token.Text);
                if (x + width > maxWidth && lineHasContent)
                {
                    y += lineHeight;
                    x = 0;
                    // Drop trailing spaces that ended the previous line.
                    if (placed.Count > 0 && !placed[placed.Count - 1].Token.IsWord && placed[placed.Count - 1].Token.Text.Trim().Length == 0)
                        placed.RemoveAt(placed.Count - 1);
                }
                placed.Add(new PlacedToken(token, block, x, y, width, lineHeight));
                x += width;
                lineHasContent = true;
            }
            return y + lineHeight;
        }
    }
}
