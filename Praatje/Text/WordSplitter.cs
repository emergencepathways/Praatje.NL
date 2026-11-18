using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PraatjeNL.Text
{
    /// <summary>A piece of text: either a word or the spaces/punctuation between words.</summary>
    public sealed class Token
    {
        public Token(string text, bool isWord, int start)
        {
            this.Text = text;
            this.IsWord = isWord;
            this.Start = start;
        }

        public string Text { get; }
        public bool IsWord { get; }
        public int Start { get; }
    }

    /// <summary>Splits Dutch text into words. Keep in step with tools/textutil.py (WORD_RE, norm).</summary>
    public static class WordSplitter
    {
        // Letters (incl. accented), with inner apostrophes or hyphens: auto's, zee-egel, 's-Gravenhage.
        private static readonly Regex Word = new(
            @"[A-Za-zÀ-ÖØ-öø-ÿ]+(?:['’\-][A-Za-zÀ-ÖØ-öø-ÿ]+)*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly char[] SentenceEnd = { '.', '!', '?', '…', '\n' };

        /// <summary>Split text into alternating word and non-word tokens that together cover the whole string.</summary>
        public static List<Token> Split(string text)
        {
            var tokens = new List<Token>();
            int pos = 0;
            foreach (Match m in Word.Matches(text))
            {
                if (m.Index > pos)
                    tokens.Add(new Token(text.Substring(pos, m.Index - pos), false, pos));
                tokens.Add(new Token(m.Value, true, m.Index));
                pos = m.Index + m.Length;
            }
            if (pos < text.Length)
                tokens.Add(new Token(text.Substring(pos), false, pos));
            return tokens;
        }

        /// <summary>The dictionary key for a word: lowercase, curly apostrophe made straight, no edge apostrophes/hyphens.</summary>
        public static string Normalize(string word)
        {
            return word.Replace('’', '\'').Trim('\'', '-').ToLowerInvariant();
        }

        /// <summary>The sentence that contains the character at <paramref name="index"/>.</summary>
        public static string SentenceAround(string text, int index)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            index = System.Math.Clamp(index, 0, text.Length - 1);
            int start = text.LastIndexOfAny(SentenceEnd, System.Math.Max(0, index - 1));
            start = start < 0 ? 0 : start + 1;
            int end = text.IndexOfAny(SentenceEnd, index);
            end = end < 0 ? text.Length : end + 1;
            if (end < text.Length && text[end - 1] == '\n')
                end--;
            string s = text.Substring(start, end - start).Trim();
            return s.Length > 300 ? s.Substring(0, 300) + "…" : s;
        }
    }
}
