using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PraatjeNL.Vocab
{
    /// <summary>
    /// Anki-ready text: the same columns as Speurtocht.NL (Nederlands, English, Zin, Tags), so both
    /// mods can share one deck and note type. Anki 2.1.54+ reads the # headers by itself.
    /// </summary>
    public static class AnkiText
    {
        public const string Tags = "PraatjeNL Stardew";

        public static string Build(IEnumerable<VocabEntry> entries, bool onlyNew, out int count)
        {
            var sb = new StringBuilder();
            sb.Append("#separator:tab\n#html:true\n#columns:Nederlands\tEnglish\tZin\tTags\n#tags column:4\n");
            count = 0;
            foreach (VocabEntry e in entries.OrderBy(e => e.Added))
            {
                if (onlyNew && e.Exported)
                    continue;
                sb.Append(Clean(e.Word)).Append('\t')
                  .Append(Clean(EnglishField(e))).Append('\t')
                  .Append(ZinField(e)).Append('\t')
                  .Append(Tags).Append('\n');
                count++;
            }
            return sb.ToString();
        }

        /// <summary>English side: meaning, plus the base form when it differs from the saved form.</summary>
        public static string EnglishField(VocabEntry e)
        {
            string m = e.Meaning ?? "";
            if (!string.IsNullOrEmpty(e.Lemma) && e.Lemma != e.Word)
                m = m.Length > 0 ? $"{m} — ({e.Lemma})" : $"({e.Lemma})";
            return m;
        }

        /// <summary>Back of the card: the sentence, then the source in italics.</summary>
        public static string ZinField(VocabEntry e)
        {
            string zin = Clean(e.Sentence);
            if (!string.IsNullOrEmpty(e.Source))
                zin += (zin.Length > 0 ? "<br>" : "") + "<i>" + Clean(e.Source) + "</i>";
            return zin;
        }

        private static string Clean(string? s) => (s ?? "").Replace('\t', ' ').Replace("\r", " ").Replace("\n", "<br>");
    }
}
