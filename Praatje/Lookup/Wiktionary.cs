using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PraatjeNL.Lookup
{
    /// <summary>
    /// Online fallback for words that aren't in the prebuilt dictionary yet: English Wiktionary's
    /// definition API, Dutch section only. Results are cached by the mod.
    /// </summary>
    public sealed class Wiktionary
    {
        private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex FormOf = new(@"\bof ([\p{L}'\-]+)\s*$", RegexOptions.Compiled);
        private readonly HttpClient http;

        public Wiktionary(string version)
        {
            this.http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd($"PraatjeNL/{version} (Stardew Valley mod; github.com/emergencepathways/Praatje.NL)");
        }

        /// <summary>Returns (lemma, meaning), or null when Wiktionary has no Dutch entry. Throws on network errors.</summary>
        public async Task<(string lemma, string meaning)?> LookupAsync(string word)
        {
            (string, string)? r = await this.FetchAsync(word);
            if (r == null && word.Length > 0 && char.IsLower(word[0]))
                return null;
            if (r == null)
                r = await this.FetchAsync(word.ToLowerInvariant());
            return r;
        }

        private async Task<(string lemma, string meaning)?> FetchAsync(string word)
        {
            string url = "https://en.wiktionary.org/api/rest_v1/page/definition/" + Uri.EscapeDataString(word) + "?redirect=true";
            using HttpResponseMessage resp = await this.http.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return null;
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync();
            return Parse(word, json);
        }

        /// <summary>Pick the first definitions from the Dutch ("nl") section. Public for the tests.</summary>
        public static (string lemma, string meaning)? Parse(string word, string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("nl", out JsonElement nl))
                return null;
            var parts = new List<string>();
            string lemma = word.ToLowerInvariant();
            foreach (JsonElement pos in nl.EnumerateArray())
            {
                string partOfSpeech = pos.TryGetProperty("partOfSpeech", out JsonElement p) ? p.GetString() ?? "" : "";
                if (!pos.TryGetProperty("definitions", out JsonElement defs))
                    continue;
                foreach (JsonElement d in defs.EnumerateArray())
                {
                    string text = d.TryGetProperty("definition", out JsonElement t) ? Strip(t.GetString() ?? "") : "";
                    if (text.Length == 0)
                        continue;
                    // "inflection of hebben" / "plural of boom": remember the base form
                    Match m = FormOf.Match(text);
                    if (m.Success && (text.Contains("inflection") || text.Contains("plural") || text.Contains("form") || text.Contains("participle") || text.Contains("diminutive") || text.Contains("singular") || text.Contains("comparative") || text.Contains("superlative")))
                        lemma = m.Groups[1].Value.ToLowerInvariant();
                    string piece = partOfSpeech.Length > 0 ? $"{partOfSpeech.ToLowerInvariant()}: {text}" : text;
                    if (!parts.Contains(piece))
                        parts.Add(piece);
                    if (parts.Count >= 3)
                        break;
                }
                if (parts.Count >= 3)
                    break;
            }
            if (parts.Count == 0)
                return null;
            string meaning = string.Join("; ", parts);
            if (meaning.Length > 220)
                meaning = meaning.Substring(0, 217) + "…";
            return (lemma, meaning);
        }

        private static string Strip(string html)
        {
            string s = WebUtility.HtmlDecode(Tags.Replace(html, ""));
            return Spaces.Replace(s, " ").Trim();
        }
    }
}
