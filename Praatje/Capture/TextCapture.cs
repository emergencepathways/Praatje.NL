using System;
using System.Collections.Generic;
using System.Linq;
using PraatjeNL.Text;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace PraatjeNL.Capture
{
    /// <summary>The text currently on screen, as the reader shows it.</summary>
    public sealed class CapturedText
    {
        public CapturedText(string kind, string source, List<string> blocks)
        {
            this.Kind = kind;
            this.Source = source;
            this.Blocks = blocks;
        }

        /// <summary>"dialogue", "event", "letter", "note", "tv", "text".</summary>
        public string Kind { get; }

        /// <summary>Who or what it comes from: "Abigail", "Brief: Robin" ... Saved with each word.</summary>
        public string Source { get; }

        /// <summary>Block 0 is the main text; later blocks are answer choices.</summary>
        public List<string> Blocks { get; }

        /// <summary>The original English text, when it could be found.</summary>
        public string? English { get; set; }

        /// <summary>Identifies this text: the reader resets its scroll position when it changes.</summary>
        public string Identity => this.Kind + "|" + this.Source + "|" + string.Join("|", this.Blocks);
    }

    /// <summary>Reads the Dutch text from the open dialogue box or letter.</summary>
    public sealed class TextCapture
    {
        private readonly IMonitor monitor;
        private readonly EnglishSource english;

        // English found for a Dutch text: capture runs several times a second, the lookup only once per text.
        private readonly Dictionary<string, string?> englishCache = new();

        public TextCapture(IMonitor monitor, EnglishSource english)
        {
            this.monitor = monitor;
            this.english = english;
        }

        public CapturedText? Capture(IClickableMenu? menu)
        {
            try
            {
                return menu switch
                {
                    DialogueBox box => this.FromDialogue(box),
                    LetterViewerMenu letter => this.FromLetter(letter),
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                this.monitor.LogOnce($"Couldn't read the text on screen ({menu?.GetType().Name}): {ex}", LogLevel.Warn);
                return null;
            }
        }

        private CapturedText? FromDialogue(DialogueBox box)
        {
            string text;
            string source;
            string kind;
            Dialogue? d = box.characterDialogue;
            if (d != null)
            {
                text = d.getCurrentDialogue() ?? "";
                source = d.speaker?.displayName ?? d.speaker?.Name ?? "";
                kind = Game1.CurrentEvent != null ? "event" : "dialogue";
            }
            else
            {
                text = box.getCurrentString() ?? "";
                source = Game1.CurrentEvent != null ? "Gebeurtenis" : "Tekst";
                kind = Game1.CurrentEvent != null ? "event" : "text";
            }
            text = Tidy(text);
            if (text.Length == 0)
                return null;

            var blocks = new List<string> { text };
            if (box.isQuestion && box.responses != null)
            {
                foreach (Response r in box.responses)
                {
                    if (!string.IsNullOrWhiteSpace(r.responseText))
                        blocks.Add("› " + Tidy(r.responseText));
                }
            }

            var captured = new CapturedText(kind, source, blocks);
            string cacheKey = (d?.TranslationKey ?? Game1.CurrentEvent?.id ?? "") + "|" + text;
            captured.English = this.Cached(cacheKey, () => this.EnglishForDialogue(d, text));
            return captured;
        }

        private string? EnglishForDialogue(Dialogue? d, string dutchText)
        {
            string player = Game1.player?.Name ?? "";
            if (d != null && !string.IsNullOrEmpty(d.TranslationKey))
            {
                string? raw = this.english.FromTranslationKey(d.TranslationKey);
                if (raw != null)
                {
                    List<string> pages = Cleaner.Pages(raw, player);
                    // Same page as the Dutch box, when both have the same number of pages
                    if (pages.Count > 0 && pages.Count == d.dialogues.Count && d.currentDialogueIndex < pages.Count)
                        return pages[d.currentDialogueIndex];
                    return string.Join("\n", pages);
                }
            }
            if (Game1.CurrentEvent != null)
            {
                string? line = this.english.ForEvent(Game1.CurrentEvent, dutchText);
                if (line != null)
                    return Cleaner.Clean(line, player);
            }
            return null;
        }

        private CapturedText? FromLetter(LetterViewerMenu letter)
        {
            if (letter.mailMessage == null || letter.mailMessage.Count == 0)
                return null;
            int page = Math.Clamp(letter.page, 0, letter.mailMessage.Count - 1);
            string text = Tidy(letter.mailMessage[page]);
            if (text.Length == 0)
                return null;

            bool isNote = !letter.isMail;
            string title = letter.mailTitle ?? "";
            string source = isNote ? "Notitie" : title.Length > 0 ? "Brief: " + title : "Brief";
            var captured = new CapturedText(isNote ? "note" : "letter", source, new List<string> { text });

            string player = Game1.player?.Name ?? "";
            captured.English = this.Cached("letter|" + title + "|" + text, () =>
            {
                string? raw = title.Length > 0 ? this.english.Get("Data/mail", title) : null;
                raw ??= this.english.ByMatchingDutch(isNote ? "Data/SecretNotes" : "Data/mail", text);
                return raw != null ? Cleaner.Clean(raw, player) : null;
            });
            return captured;
        }

        private string? Cached(string key, Func<string?> find)
        {
            if (this.englishCache.TryGetValue(key, out string? hit))
                return hit;
            if (this.englishCache.Count > 300)
                this.englishCache.Clear();
            string? found = find();
            this.englishCache[key] = found;
            return found;
        }

        /// <summary>Text as shown in game: no leftover codes, normal spacing.</summary>
        private static string Tidy(string s)
        {
            s = s.Replace("^", "\n").Replace("\r", "");
            s = string.Join("\n", s.Split('\n').Select(l => l.Trim()));
            while (s.Contains("\n\n\n"))
                s = s.Replace("\n\n\n", "\n\n");
            return s.Trim();
        }
    }
}
