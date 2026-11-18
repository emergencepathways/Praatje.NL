using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PraatjeNL.Text
{
    /// <summary>
    /// Turns raw Stardew asset text (with dialogue commands) into readable text.
    /// Used for the English column, which is read straight from the game's content files.
    /// Keep in step with tools/textutil.py (clean).
    /// </summary>
    public static class Cleaner
    {
        private static readonly Regex Item = new(@"%item[^%]*%%", RegexOptions.Compiled);
        private static readonly Regex MailTitle = new(@"\[#\].*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GenderBlock = new(@"\$\{([^}]*)\}\$", RegexOptions.Compiled);
        private static readonly Regex Command = new(@"\$[a-z](?:\s*\d+)?", RegexOptions.Compiled);
        private static readonly Regex Portrait = new(@"\$\d+", RegexOptions.Compiled);
        private static readonly Regex Tokens = new(@"\{\{[^}]*\}\}|\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"[ \t]+", RegexOptions.Compiled);
        private static readonly Regex Breaks = new(@"\s*\n\s*", RegexOptions.Compiled);
        private static readonly Regex PageBreak = new(@"#\$[be]#", RegexOptions.Compiled);
        private static readonly Regex EventText = new(
            "(?:speak|message|textAboveHead|end dialogue|question|quickQuestion|splitSpeak|showText)\\b[^\"]*\"([^\"]*)\"",
            RegexOptions.Compiled);

        /// <summary>Split a dialogue entry into its pages (#$b# / #$e#), each cleaned.</summary>
        public static List<string> Pages(string raw, string? playerName = null)
        {
            var pages = new List<string>();
            foreach (string part in PageBreak.Split(raw ?? ""))
            {
                string c = Clean(part, playerName);
                if (c.Length > 0)
                    pages.Add(c);
            }
            return pages;
        }

        /// <summary>Remove dialogue commands, keep readable text. '^' becomes a line break.</summary>
        public static string Clean(string raw, string? playerName = null)
        {
            string s = raw ?? "";
            s = Item.Replace(s, " ");
            s = MailTitle.Replace(s, "");
            s = GenderBlock.Replace(s, m => m.Groups[1].Value.Split('^', '¦')[0]);
            if (s.Contains('¦'))
                s = s.Split('¦')[0];
            s = s.Replace("#$b#", "\n").Replace("#$e#", "\n");
            s = Command.Replace(s, "");
            s = Portrait.Replace(s, "");
            s = Tokens.Replace(s, "");
            s = s.Replace('^', '\n').Replace("#", " ");
            s = s.Replace("@", string.IsNullOrEmpty(playerName) ? "…" : playerName);
            s = Spaces.Replace(s, " ");
            s = Breaks.Replace(s, "\n");
            return s.Trim();
        }

        /// <summary>The spoken/shown text strings in an event script, in order.</summary>
        public static List<string> EventStrings(string script)
        {
            var list = new List<string>();
            foreach (Match m in EventText.Matches(script ?? ""))
                list.Add(m.Groups[1].Value);
            return list;
        }
    }
}
