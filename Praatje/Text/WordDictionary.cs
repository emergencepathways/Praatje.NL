using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PraatjeNL.Text
{
    /// <summary>One dictionary entry: the word's base form and its English meaning.</summary>
    public sealed class DictEntry
    {
        public DictEntry(string lemma, string meaning, bool guessed = false)
        {
            this.Lemma = lemma;
            this.Meaning = meaning;
            this.Guessed = guessed;
        }

        public string Lemma { get; }
        public string Meaning { get; }

        /// <summary>True when found through a spelling rule rather than an exact match.</summary>
        public bool Guessed { get; }
    }

    /// <summary>
    /// The prebuilt word list (assets/dictionary.json, generated from batches/*.json by tools/import_batch.py).
    /// Format: { "words": { "form": ["lemma", "meaning"], ... }, "names": ["name", ...] }
    /// </summary>
    public sealed class WordDictionary
    {
        private readonly Dictionary<string, DictEntry> words = new(StringComparer.Ordinal);
        private readonly HashSet<string> names = new(StringComparer.Ordinal);

        public int Count => this.words.Count;

        public static WordDictionary FromJson(string json)
        {
            var dict = new WordDictionary();
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("words", out JsonElement words))
            {
                foreach (JsonProperty p in words.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() < 2)
                        continue;
                    string lemma = p.Value[0].GetString() ?? p.Name;
                    string meaning = p.Value[1].GetString() ?? "";
                    dict.words[WordSplitter.Normalize(p.Name)] = new DictEntry(lemma, meaning);
                }
            }
            if (doc.RootElement.TryGetProperty("names", out JsonElement names))
            {
                foreach (JsonElement n in names.EnumerateArray())
                    dict.names.Add(WordSplitter.Normalize(n.GetString() ?? ""));
            }
            return dict;
        }

        public void Add(string form, string lemma, string meaning)
        {
            this.words[WordSplitter.Normalize(form)] = new DictEntry(lemma, meaning);
        }

        public bool IsName(string word) => this.names.Contains(WordSplitter.Normalize(word));

        /// <summary>Find a word: exact form first, then a few common Dutch spelling rules (marked as guessed).</summary>
        public DictEntry? Lookup(string word)
        {
            string key = WordSplitter.Normalize(word);
            if (key.Length == 0)
                return null;
            if (this.words.TryGetValue(key, out DictEntry? exact))
                return exact;

            // Hyphenated compounds: look up the last part (zee-egel -> egel).
            int dash = key.LastIndexOf('-');
            if (dash > 0 && dash < key.Length - 1 && this.words.TryGetValue(key.Substring(dash + 1), out DictEntry? tail))
                return new DictEntry(tail.Lemma, tail.Meaning, guessed: true);

            foreach ((string suffix, string replace, string note) in Rules)
            {
                if (key.Length > suffix.Length + 2 && key.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string stem = key.Substring(0, key.Length - suffix.Length) + replace;
                    if (this.words.TryGetValue(stem, out DictEntry? e))
                        return new DictEntry(e.Lemma, note.Length > 0 ? $"{e.Meaning} ({note})" : e.Meaning, guessed: true);
                }
            }
            return null;
        }

        // suffix, replacement, note. Checked in order; only used when the exact form is missing.
        private static readonly (string, string, string)[] Rules =
        {
            ("'s", "", "plural"),
            ("tjes", "", "little ones"),
            ("pjes", "", "little ones"),
            ("jes", "", "little ones"),
            ("tje", "", "little"),
            ("pje", "", "little"),
            ("je", "", "little"),
            ("en", "", "plural / verb form"),
            ("s", "", "plural"),
            ("e", "", ""),
            ("er", "", "more"),
            ("st", "", "most"),
            ("de", "en", "past tense"),
            ("te", "en", "past tense"),
            ("t", "en", "verb form"),
        };
    }
}
