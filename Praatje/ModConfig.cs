using StardewModdingAPI.Utilities;

namespace PraatjeNL
{
    /// <summary>Settings in config.json (created on first launch, next to the mod's DLL).</summary>
    public sealed class ModConfig
    {
        /// <summary>Show or hide the reader.</summary>
        public KeybindList ToggleReader { get; set; } = KeybindList.Parse("F9");

        /// <summary>Show or hide the word list ("Mijn woorden").</summary>
        public KeybindList ToggleWordList { get; set; } = KeybindList.Parse("F10");

        /// <summary>Open the reader by itself when a dialogue box or letter appears.</summary>
        public bool AutoOpenReader { get; set; } = true;

        /// <summary>Show the original English next to the Dutch.</summary>
        public bool ShowEnglish { get; set; } = false;

        /// <summary>Text size in the reader (1 = the game's small font).</summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>Width of one text column, in screen pixels.</summary>
        public int ReaderColumnWidth { get; set; } = 520;

        /// <summary>Largest reader height, as a share of the screen height.</summary>
        public float ReaderMaxHeight { get; set; } = 0.55f;

        /// <summary>Reader position (drag its title bar to move it). -1 = default.</summary>
        public int ReaderX { get; set; } = -1;
        public int ReaderY { get; set; } = -1;

        /// <summary>Look up words that aren't in the built-in dictionary on English Wiktionary.</summary>
        public bool OnlineLookup { get; set; } = true;

        /// <summary>Send saved words to Anki through AnkiConnect.</summary>
        public bool AnkiEnabled { get; set; } = true;
        public string AnkiUrl { get; set; } = "http://127.0.0.1:8765";

        /// <summary>Anki deck. The same deck as Speurtocht.NL by default; the note type is shared.</summary>
        public string AnkiDeck { get; set; } = "Speurtocht.NL";

        /// <summary>Folder for "Exporteer .txt". Empty = Documents/PraatjeNL.</summary>
        public string ExportFolder { get; set; } = "";
    }
}
