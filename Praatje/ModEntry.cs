using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;
using PraatjeNL.Capture;
using PraatjeNL.Lookup;
using PraatjeNL.Text;
using PraatjeNL.UI;
using PraatjeNL.Vocab;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace PraatjeNL
{
    /// <summary>A Wiktionary result kept between sessions. Meaning "" = Wiktionary has no Dutch entry.</summary>
    public sealed class OnlineEntry
    {
        public string Lemma { get; set; } = "";
        public string Meaning { get; set; } = "";
    }

    /// <summary>Praatje.NL: click Dutch words in dialogue and letters, save them, send them to Anki.</summary>
    public sealed class ModEntry : Mod
    {
        private const string VocabKey = "vocab";
        private const string OnlineKey = "wiktionary-cache";

        public ModConfig Config { get; private set; } = new();
        public VocabStore Vocab { get; private set; } = new();
        internal ReaderPanel Reader { get; private set; } = null!;
        internal WordListPanel WordList { get; private set; } = null!;

        private WordDictionary dictionary = new();
        private Dictionary<string, OnlineEntry> online = new();
        private readonly HashSet<string> searching = new();
        private readonly Dictionary<string, DateTime> searchFailed = new();
        private readonly HashSet<string> names = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<Action> mainThread = new();
        private TextCapture capture = null!;
        private EnglishSource english = null!;
        private Wiktionary wiktionary = null!;
        private AnkiConnect? anki;
        private bool ankiBusy;
        private DateTime ankiRetryAfter = DateTime.MinValue;
        private bool readerOn = true;
        private bool readerClosedForThisText;
        private IClickableMenu? lastMenu;

        /// <summary>Mouse buttons pressed on one of our panels; hidden from the game until they are released.</summary>
        private readonly HashSet<SButton> heldOnPanel = new();
        private string status = "";
        private DateTime statusUntil;

        public int AnkiWaiting => this.Vocab.Entries.Count(e => e.SentToAnki == null || e.SentToAnki != e.Meaning);

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.readerOn = this.Config.AutoOpenReader;
            this.Vocab = helper.Data.ReadGlobalData<VocabStore>(VocabKey) ?? new VocabStore();
            this.online = helper.Data.ReadGlobalData<Dictionary<string, OnlineEntry>>(OnlineKey) ?? new Dictionary<string, OnlineEntry>();
            this.LoadDictionary();

            this.english = new EnglishSource(this.Monitor);
            this.capture = new TextCapture(this.Monitor, this.english);
            this.wiktionary = new Wiktionary(this.ModManifest.Version.ToString());
            this.Reader = new ReaderPanel(this);
            this.WordList = new WordListPanel(this);

            helper.Events.GameLoop.UpdateTicking += this.OnUpdateTicking;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.DayStarted += (_, _) => this.CollectNames();
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.OnSaveLoaded();
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Input.MouseWheelScrolled += this.OnMouseWheelScrolled;
            helper.Events.Content.LocaleChanged += (_, _) => this.english.ClearDutchCache();

            helper.ConsoleCommands.Add("praatje", "Praatje.NL. Usage:\n  praatje status   - dictionary size, saved words, Anki queue\n  praatje anki     - send unsent words to Anki now\n  praatje export   - write the Anki .txt file", this.OnCommand);
        }

        // ------------------------------------------------------------------
        // Setup
        // ------------------------------------------------------------------
        private void LoadDictionary()
        {
            string path = Path.Combine(this.Helper.DirectoryPath, "assets", "dictionary.json");
            try
            {
                this.dictionary = WordDictionary.FromJson(File.ReadAllText(path));
                this.Monitor.Log($"Woordenboek geladen: {this.dictionary.Count} woordvormen.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Couldn't load {path}: {ex.Message}. Only online lookups will work.", LogLevel.Warn);
            }
        }

        private void OnSaveLoaded()
        {
            this.CollectNames();
            string? lang = LocalizedContentManager.CurrentModLanguage?.LanguageCode;
            if (LocalizedContentManager.CurrentLanguageCode != LocalizedContentManager.LanguageCode.mod || lang != "nl")
                this.Monitor.Log("The game language isn't Dutch (Nederlands). Choose it on the title screen's language page 2 (StardewValley Dutch mod); Praatje.NL reads whatever text is on screen.", LogLevel.Info);
        }

        /// <summary>Villager, player, farm and pet names: shown in red and never saved.</summary>
        private void CollectNames()
        {
            this.names.Clear();
            try
            {
                foreach (string key in Game1.characterData.Keys)
                    this.names.Add(WordSplitter.Normalize(key));
                foreach (NPC npc in Utility.getAllCharacters())
                {
                    this.names.Add(WordSplitter.Normalize(npc.Name ?? ""));
                    foreach (string part in (npc.displayName ?? "").Split(' '))
                        this.names.Add(WordSplitter.Normalize(part));
                }
                if (Game1.player != null)
                {
                    this.names.Add(WordSplitter.Normalize(Game1.player.Name ?? ""));
                    this.names.Add(WordSplitter.Normalize(Game1.player.farmName.Value ?? ""));
                    this.names.Add(WordSplitter.Normalize(Game1.player.getPetName() ?? ""));
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Couldn't collect names: {ex.Message}", LogLevel.Trace);
            }
            this.names.Remove("");
        }

        public void SaveConfig() => this.Helper.WriteConfig(this.Config);

        private void SaveVocab() => this.Helper.Data.WriteGlobalData(VocabKey, this.Vocab);

        // ------------------------------------------------------------------
        // Words
        // ------------------------------------------------------------------
        internal WordInfo Describe(string word, bool lookupOnline)
        {
            string key = WordSplitter.Normalize(word);
            var info = new WordInfo { Word = word, Lemma = key };
            bool capitalised = word.Length > 0 && char.IsUpper(word[0]);
            if (capitalised && (this.names.Contains(key) || this.dictionary.IsName(key)))
            {
                info.IsName = true;
                return info;
            }
            info.IsSaved = this.Vocab.Contains(key);
            DictEntry? entry = this.dictionary.Lookup(key);
            if (entry != null && !(entry.Guessed && this.online.TryGetValue(key, out OnlineEntry? better) && better.Meaning.Length > 0))
            {
                info.Lemma = entry.Lemma;
                info.Meaning = entry.Meaning;
                return info;
            }
            if (this.online.TryGetValue(key, out OnlineEntry? o))
            {
                info.Lemma = o.Lemma.Length > 0 ? o.Lemma : key;
                info.Meaning = o.Meaning;
                return info;
            }
            info.Searching = this.searching.Contains(key);
            if (lookupOnline)
                info.Searching = this.StartLookup(key) || info.Searching;
            return info;
        }

        /// <summary>Start a Wiktionary lookup in the background. Returns true if one is running.</summary>
        private bool StartLookup(string key)
        {
            if (!this.Config.OnlineLookup || key.Length < 2 || this.searching.Contains(key))
                return this.searching.Contains(key);
            if (this.searchFailed.TryGetValue(key, out DateTime failedAt) && DateTime.Now - failedAt < TimeSpan.FromMinutes(5))
                return false;
            this.searching.Add(key);
            Task.Run(async () =>
            {
                try
                {
                    (string lemma, string meaning)? r = await this.wiktionary.LookupAsync(key);
                    this.mainThread.Enqueue(() => this.OnLookupDone(key, r));
                }
                catch (Exception ex)
                {
                    this.mainThread.Enqueue(() =>
                    {
                        this.searching.Remove(key);
                        this.searchFailed[key] = DateTime.Now;
                        this.Monitor.Log($"Wiktionary lookup for '{key}' failed: {ex.Message}", LogLevel.Trace);
                    });
                }
            });
            return true;
        }

        private void OnLookupDone(string key, (string lemma, string meaning)? result)
        {
            this.searching.Remove(key);
            this.online[key] = new OnlineEntry { Lemma = result?.lemma ?? key, Meaning = result?.meaning ?? "" };
            this.Helper.Data.WriteGlobalData(OnlineKey, this.online);
            VocabEntry? saved = this.Vocab.Find(key);
            if (saved != null && saved.Meaning.Length == 0 && (result?.meaning ?? "").Length > 0)
            {
                saved.Meaning = result!.Value.meaning;
                saved.Lemma = result.Value.lemma;
                this.SaveVocab();
                this.SendAllToAnki(manual: false);
            }
        }

        internal void OnWordClicked(string word, string sentence, string source, bool right)
        {
            WordInfo info = this.Describe(word, lookupOnline: true);
            if (info.IsName)
            {
                this.ShowStatus($"{word} is een naam - niet opgeslagen.");
                return;
            }
            if (right)
            {
                this.RemoveWord(word);
                return;
            }
            (VocabEntry entry, bool isNew) = this.Vocab.Save(word, info.Lemma, info.Meaning, sentence, source);
            this.SaveVocab();
            this.ShowStatus(isNew
                ? $"'{entry.Word}' opgeslagen{(entry.Meaning.Length > 0 ? " - " + entry.Meaning : "")}"
                : $"'{entry.Word}' stond al in je lijst ({entry.Seen}x gezien).");
            if (isNew)
                this.SendAllToAnki(manual: false);
        }

        internal void RemoveWord(string word)
        {
            if (this.Vocab.Remove(word))
            {
                this.SaveVocab();
                this.ShowStatus($"'{WordSplitter.Normalize(word)}' verwijderd.");
            }
        }

        // ------------------------------------------------------------------
        // Anki and export
        // ------------------------------------------------------------------
        internal void SendAllToAnki(bool manual)
        {
            if (!this.Config.AnkiEnabled)
            {
                if (manual)
                    this.ShowStatus("Anki staat uit (AnkiEnabled in config.json).");
                return;
            }
            if (this.ankiBusy || (!manual && DateTime.Now < this.ankiRetryAfter))
                return;
            List<VocabEntry> todo = this.Vocab.Entries.Where(e => e.SentToAnki == null || e.SentToAnki != e.Meaning).OrderBy(e => e.Added).ToList();
            if (todo.Count == 0)
            {
                if (manual)
                    this.ShowStatus("Alle woorden staan al in Anki.");
                return;
            }
            this.anki ??= new AnkiConnect(this.Config.AnkiUrl, this.Config.AnkiDeck);
            this.ankiBusy = true;
            AnkiConnect client = this.anki;
            Task.Run(async () =>
            {
                int added = 0, updated = 0;
                string? error = null;
                foreach (VocabEntry e in todo)
                {
                    try
                    {
                        string result = await client.AddOrUpdateAsync(e);
                        if (result == "added") added++;
                        else if (result == "updated") updated++;
                        string meaning = e.Meaning;
                        this.mainThread.Enqueue(() => e.SentToAnki = meaning);
                    }
                    catch (Exception ex)
                    {
                        error = ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException
                            ? "Anki niet bereikbaar. Is Anki open, met AnkiConnect (code 2055492159)?"
                            : ex.Message;
                        break;
                    }
                }
                this.mainThread.Enqueue(() =>
                {
                    this.ankiBusy = false;
                    this.SaveVocab();
                    if (error != null)
                    {
                        this.ankiRetryAfter = DateTime.Now.AddMinutes(2);
                        this.Monitor.Log("Anki: " + error, manual ? LogLevel.Warn : LogLevel.Trace);
                        if (manual)
                            this.ShowStatus(error);
                    }
                    else if (added + updated > 0 || manual)
                        this.ShowStatus($"Anki: {added} nieuw, {updated} bijgewerkt.");
                });
            });
        }

        internal void ExportText(bool toClipboard)
        {
            string text = AnkiText.Build(this.Vocab.Entries, onlyNew: false, out int count);
            if (count == 0)
            {
                this.ShowStatus("Nog geen woorden om te exporteren.");
                return;
            }
            if (toClipboard)
            {
                bool ok = DesktopClipboard.SetText(text);
                this.ShowStatus(ok ? $"{count} woorden gekopieerd." : "Kopiëren lukte niet (installeer xclip of wl-clipboard). Gebruik Exporteer .txt.");
                return;
            }
            string folder = this.Config.ExportFolder.Length > 0
                ? this.Config.ExportFolder
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PraatjeNL");
            try
            {
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"praatje-woorden-{DateTime.Now:yyyy-MM-dd}.txt");
                File.WriteAllText(file, text, new System.Text.UTF8Encoding(false));
                foreach (VocabEntry e in this.Vocab.Entries)
                    e.Exported = true;
                this.SaveVocab();
                this.Monitor.Log($"Exported {count} words to {file}", LogLevel.Info);
                this.ShowStatus($"{count} woorden opgeslagen in {file}");
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Export failed: {ex}", LogLevel.Error);
                this.ShowStatus("Exporteren mislukt: " + ex.Message);
            }
        }

        private void OnCommand(string name, string[] args)
        {
            switch (args.FirstOrDefault())
            {
                case "anki":
                    this.SendAllToAnki(manual: true);
                    break;
                case "export":
                    this.ExportText(toClipboard: false);
                    break;
                default:
                    this.Monitor.Log($"Dictionary: {this.dictionary.Count} word forms. Wiktionary cache: {this.online.Count}. Saved words: {this.Vocab.Entries.Count}, waiting for Anki: {this.AnkiWaiting}.", LogLevel.Info);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Reader state
        // ------------------------------------------------------------------
        internal void HideReader()
        {
            this.readerClosedForThisText = true;
        }

        internal void ToggleWordList()
        {
            this.WordList.Open = !this.WordList.Open;
        }

        private void ShowStatus(string message)
        {
            this.status = message;
            this.statusUntil = DateTime.Now.AddSeconds(4);
            this.Monitor.Log(message, LogLevel.Trace);
            if (!this.ReaderVisible && !this.WordList.Open)
                Game1.addHUDMessage(new HUDMessage(message, 2));
        }

        private bool ReaderVisible => this.readerOn && !this.readerClosedForThisText && this.Reader.HasText;

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is not DialogueBox && e.NewMenu is not LetterViewerMenu)
                this.readerClosedForThisText = false;
        }

        /// <summary>
        /// SMAPI only hides a suppressed button from the game for the tick it was pressed. The game compares this tick's
        /// mouse with last tick's, so a button still held on the next tick looks like a fresh click and advances the
        /// dialogue. Keep suppressing a click that started on our panel until the player lets go.
        /// </summary>
        private void OnUpdateTicking(object? sender, UpdateTickingEventArgs e)
        {
            if (this.heldOnPanel.Count == 0)
                return;
            MouseState mouse = Mouse.GetState();
            foreach (SButton button in this.heldOnPanel.ToList())
            {
                bool down = button == SButton.MouseLeft ? mouse.LeftButton == ButtonState.Pressed
                    : button == SButton.MouseRight ? mouse.RightButton == ButtonState.Pressed
                    : this.Helper.Input.IsDown(button);
                if (down)
                {
                    this.Helper.Input.Suppress(button);
                    HoldDialogue();
                }
                else
                    this.heldOnPanel.Remove(button);
            }
        }

        /// <summary>Backup: a dialogue box ignores clicks while its safety timer runs.</summary>
        private static void HoldDialogue()
        {
            if (Game1.activeClickableMenu is DialogueBox box && box.safetyTimer < 250)
                box.safetyTimer = 250;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            while (this.mainThread.TryDequeue(out Action? action))
                action();

            IClickableMenu? menu = Game1.activeClickableMenu;
            if (menu != this.lastMenu || e.IsMultipleOf(6))
            {
                this.lastMenu = menu;
                this.Reader.SetText(this.capture.Capture(menu));
            }

            if (this.ReaderVisible)
            {
                MouseState mouse = Mouse.GetState();
                this.Reader.Update(Game1.getMouseX(true), Game1.getMouseY(true), mouse.LeftButton == ButtonState.Pressed);
            }

            if (e.IsMultipleOf(60 * 60) && this.AnkiWaiting > 0)
                this.SendAllToAnki(manual: false);
        }

        // ------------------------------------------------------------------
        // Drawing and input
        // ------------------------------------------------------------------
        private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (!this.ReaderVisible && !this.WordList.Open)
                return;
            if (this.ReaderVisible)
                this.Reader.Draw(e.SpriteBatch);
            this.WordList.Draw(e.SpriteBatch);
            this.DrawStatus(e.SpriteBatch);
            if (this.ReaderVisible)
                this.Reader.DrawTooltip(e.SpriteBatch);
            this.WordList.DrawTooltip(e.SpriteBatch);
            Game1.activeClickableMenu?.drawMouse(e.SpriteBatch, false, -1);
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (Game1.activeClickableMenu != null || !this.WordList.Open)
                return;
            this.WordList.Draw(e.SpriteBatch);
            this.DrawStatus(e.SpriteBatch);
            this.WordList.DrawTooltip(e.SpriteBatch);
        }

        private void DrawStatus(Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            if (DateTime.Now > this.statusUntil || this.status.Length == 0)
                return;
            Microsoft.Xna.Framework.Rectangle anchor = this.ReaderVisible ? this.Reader.Bounds : this.WordList.Bounds;
            float x = anchor.X + 24;
            float y = anchor.Bottom + 6;
            string text = Game1.parseText(this.status, Game1.smallFont, System.Math.Max(300, anchor.Width - 48));
            Microsoft.Xna.Framework.Vector2 size = Game1.smallFont.MeasureString(Ui.Safe(Game1.smallFont, text)) * 0.85f;
            Ui.Box(b, new Microsoft.Xna.Framework.Rectangle((int)x - 16, (int)y, (int)size.X + 32, (int)size.Y + 24));
            Ui.Text(b, Game1.smallFont, text, x, y + 12, Game1.textColor, 0.85f);
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady && Game1.activeClickableMenu is not LetterViewerMenu)
                return;

            if (this.Config.ToggleReader.JustPressed())
            {
                if (this.readerClosedForThisText)
                {
                    this.readerClosedForThisText = false;
                    this.readerOn = true;
                }
                else
                    this.readerOn = !this.readerOn;
                this.ShowStatus(this.readerOn ? "Lezer aan (F9)" : "Lezer uit (F9)");
                return;
            }
            if (this.Config.ToggleWordList.JustPressed())
            {
                this.ToggleWordList();
                return;
            }

            if (e.Button != SButton.MouseLeft && e.Button != SButton.MouseRight)
                return;
            int x = Game1.getMouseX(true), y = Game1.getMouseY(true);
            bool right = e.Button == SButton.MouseRight;
            bool handled = this.WordList.Click(x, y, right);
            if (!handled && this.ReaderVisible)
                handled = this.Reader.Click(x, y, right);
            if (handled)
            {
                this.Helper.Input.Suppress(e.Button);
                this.heldOnPanel.Add(e.Button);
                HoldDialogue();
            }
        }

        private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
        {
            int x = Game1.getMouseX(true), y = Game1.getMouseY(true);
            int delta = e.NewValue - e.OldValue;
            if (!this.WordList.Scroll(x, y, delta) && this.ReaderVisible)
                this.Reader.Scroll(x, y, delta);
        }
    }
}
