using System;
using System.Collections.Generic;
using System.Linq;
using PraatjeNL.Text;

namespace PraatjeNL.Vocab
{
    /// <summary>A saved word. Same fields as Speurtocht.NL's word list (word, meaning, sentence, source).</summary>
    public sealed class VocabEntry
    {
        public string Word { get; set; } = "";
        public string Lemma { get; set; } = "";
        public string Meaning { get; set; } = "";
        public string Sentence { get; set; } = "";

        /// <summary>Where it was found: "Abigail", "Brief: Robin", ...</summary>
        public string Source { get; set; } = "";
        public DateTime Added { get; set; } = DateTime.Now;
        public int Seen { get; set; } = 1;

        /// <summary>Meaning that was last sent to Anki ("" = not sent yet).</summary>
        public string? SentToAnki { get; set; }
        public bool Exported { get; set; }
    }

    /// <summary>The saved word list. Persisted by the mod through SMAPI's global data (survives mod updates).</summary>
    public sealed class VocabStore
    {
        public List<VocabEntry> Entries { get; set; } = new();

        public VocabEntry? Find(string word)
        {
            string key = WordSplitter.Normalize(word);
            return this.Entries.FirstOrDefault(e => e.Word == key);
        }

        public bool Contains(string word) => this.Find(word) != null;

        /// <summary>Add a word, or count it again if already saved. Returns the entry and whether it is new.</summary>
        public (VocabEntry entry, bool isNew) Save(string word, string lemma, string meaning, string sentence, string source)
        {
            string key = WordSplitter.Normalize(word);
            VocabEntry? e = this.Find(key);
            if (e != null)
            {
                e.Seen++;
                if (string.IsNullOrEmpty(e.Meaning) && !string.IsNullOrEmpty(meaning))
                    e.Meaning = meaning;
                if (string.IsNullOrEmpty(e.Lemma) && !string.IsNullOrEmpty(lemma))
                    e.Lemma = lemma;
                return (e, false);
            }
            e = new VocabEntry { Word = key, Lemma = lemma ?? "", Meaning = meaning ?? "", Sentence = sentence ?? "", Source = source ?? "" };
            this.Entries.Add(e);
            return (e, true);
        }

        public bool Remove(string word)
        {
            VocabEntry? e = this.Find(word);
            return e != null && this.Entries.Remove(e);
        }

        /// <summary>Newest first.</summary>
        public List<VocabEntry> Sorted() => this.Entries.OrderByDescending(e => e.Added).ToList();
    }
}
