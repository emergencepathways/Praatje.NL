using System;
using System.Collections.Generic;
using System.Linq;
using PraatjeNL.Text;
using StardewModdingAPI;
using StardewValley;

namespace PraatjeNL.Capture
{
    /// <summary>
    /// Reads the game's original English text straight from its content files, bypassing SMAPI
    /// (and so the Dutch mod's replacements), for the reader's English column.
    /// </summary>
    public sealed class EnglishSource
    {
        private readonly IMonitor monitor;
        private LocalizedContentManager? raw;
        private readonly Dictionary<string, Dictionary<string, string>?> englishCache = new();
        private readonly Dictionary<string, Dictionary<string, string>?> dutchCache = new();

        public EnglishSource(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        /// <summary>Forget cached Dutch assets (they can change when the language changes).</summary>
        public void ClearDutchCache() => this.dutchCache.Clear();

        /// <summary>English entry by asset and key, e.g. ("Characters/Dialogue/Abigail", "Mon").</summary>
        public string? Get(string asset, string key)
        {
            Dictionary<string, string>? d = this.LoadEnglish(asset);
            return d != null && d.TryGetValue(key, out string? v) ? v : null;
        }

        /// <summary>English for a Dialogue.TranslationKey such as "Characters\Dialogue\Abigail:Mon".</summary>
        public string? FromTranslationKey(string? translationKey)
        {
            if (string.IsNullOrEmpty(translationKey))
                return null;
            int colon = translationKey.IndexOf(':');
            if (colon <= 0)
                return null;
            return this.Get(translationKey.Substring(0, colon), translationKey.Substring(colon + 1));
        }

        /// <summary>
        /// Find the key of the Dutch entry that contains <paramref name="dutchText"/> in a Dutch asset,
        /// then return the English entry with the same key.
        /// </summary>
        public string? ByMatchingDutch(string asset, string dutchText)
        {
            Dictionary<string, string>? nl = this.LoadDutch(asset);
            if (nl == null)
                return null;
            string probe = Probe(dutchText);
            if (probe.Length < 6)
                return null;
            foreach (KeyValuePair<string, string> pair in nl)
            {
                if (Probe(Cleaner.Clean(pair.Value)).Contains(probe, StringComparison.Ordinal))
                    return this.Get(asset, pair.Key);
            }
            return null;
        }

        /// <summary>English line for the current event: same position in the English script as the Dutch line in the Dutch script.</summary>
        public string? ForEvent(Event ev, string dutchText)
        {
            if (string.IsNullOrEmpty(ev.fromAssetName) || string.IsNullOrEmpty(ev.id))
                return null;
            string asset = ev.fromAssetName.Replace('\\', '/');
            string? nlScript = FindEventScript(this.LoadDutch(asset), ev.id);
            string? enScript = FindEventScript(this.LoadEnglish(asset), ev.id);
            if (nlScript == null || enScript == null)
                return null;
            List<string> nlLines = Cleaner.EventStrings(nlScript);
            List<string> enLines = Cleaner.EventStrings(enScript);
            string probe = Probe(dutchText);
            for (int i = 0; i < nlLines.Count && i < enLines.Count; i++)
            {
                if (probe.Length > 0 && Probe(Cleaner.Clean(nlLines[i])).Contains(probe, StringComparison.Ordinal))
                    return enLines[i];
            }
            return null;
        }

        private static string? FindEventScript(Dictionary<string, string>? scripts, string id)
        {
            if (scripts == null)
                return null;
            foreach (KeyValuePair<string, string> pair in scripts)
            {
                string key = pair.Key;
                int slash = key.IndexOf('/');
                if ((slash < 0 ? key : key.Substring(0, slash)) == id)
                    return pair.Value;
            }
            return null;
        }

        /// <summary>A comparable fingerprint: letters only, lowercase, first 60 characters of the first page.</summary>
        private static string Probe(string text)
        {
            string first = text.Split('\n')[0];
            var chars = first.Where(char.IsLetter).Select(char.ToLowerInvariant).Take(60).ToArray();
            return new string(chars);
        }

        private Dictionary<string, string>? LoadEnglish(string asset)
        {
            asset = asset.Replace('\\', '/');
            if (this.englishCache.TryGetValue(asset, out Dictionary<string, string>? cached))
                return cached;
            Dictionary<string, string>? result = null;
            try
            {
                this.raw ??= new LocalizedContentManager(Game1.content.ServiceProvider, Game1.content.RootDirectory);
                result = ToStringDict(() => this.raw.Load<Dictionary<string, string>>(asset, LocalizedContentManager.LanguageCode.en),
                                      () => this.raw.Load<Dictionary<int, string>>(asset, LocalizedContentManager.LanguageCode.en));
            }
            catch (Exception ex)
            {
                this.monitor.Log($"No English source for {asset}: {ex.Message}", LogLevel.Trace);
            }
            this.englishCache[asset] = result;
            return result;
        }

        private Dictionary<string, string>? LoadDutch(string asset)
        {
            asset = asset.Replace('\\', '/');
            if (this.dutchCache.TryGetValue(asset, out Dictionary<string, string>? cached))
                return cached;
            Dictionary<string, string>? result = null;
            try
            {
                result = ToStringDict(() => Game1.content.Load<Dictionary<string, string>>(asset),
                                      () => Game1.content.Load<Dictionary<int, string>>(asset));
            }
            catch (Exception ex)
            {
                this.monitor.Log($"Can't read {asset}: {ex.Message}", LogLevel.Trace);
            }
            this.dutchCache[asset] = result;
            return result;
        }

        private static Dictionary<string, string>? ToStringDict(Func<Dictionary<string, string>> asStrings, Func<Dictionary<int, string>> asInts)
        {
            try
            {
                return new Dictionary<string, string>(asStrings());
            }
            catch (Exception)
            {
                return asInts().ToDictionary(p => p.Key.ToString(), p => p.Value);
            }
        }
    }
}
