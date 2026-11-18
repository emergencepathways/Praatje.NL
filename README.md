# Praatje.NL

**Click any Dutch word in Stardew Valley to see what it means, save it to your word list, and send it to Anki.**

Praatje.NL ("a little chat") is a SMAPI mod for people learning Dutch. Play Stardew in Dutch, and a reader panel shows the
dialogue or letter on screen with every word clickable. It's the Stardew sister of [Speurtocht.NL](https://github.com/emergencepathways/NLQuest)
(Dutch quest text for World of Warcraft) and uses the same Anki deck.

- **Reader:** the text on screen, word by word. Hover a word for its meaning; click to save it.
- **English column:** the game's original English line next to the Dutch (**+ EN**).
- **Mijn woorden:** your saved words, each with its meaning, the sentence you found it in, and who said it.
- **Straight to Anki:** with Anki open, saved words become cards within seconds. No copy and paste.
- **Export:** an Anki-ready `.txt` file, or the same text on your clipboard for any document.
- **Names stay red:** villager, farm and pet names are marked and never saved.

**Version 0.1.0** · Stardew Valley 1.6 · SMAPI 4.x · Windows, macOS, Linux

---

## 1. Install

1. Install [SMAPI](https://smapi.io).
2. Install **[Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915)** and the **[StardewValley Dutch](https://www.nexusmods.com/stardewvalley/mods/24290)** translation, and choose **Nederlands** on page 2 of the title screen's language menu.
3. Download `PraatjeNL-<version>.zip` from [Releases](https://github.com/emergencepathways/Praatje.NL/releases) and unzip it into `Stardew Valley/Mods`. You should get `Mods/PraatjeNL/PraatjeNL.dll`.
4. Start the game through SMAPI. The SMAPI window lists **Praatje.NL**.

## 2. Reading

Talk to a villager or open a letter: the reader opens by itself (top left; drag its title bar to move it).

| Do this | What happens |
|---|---|
| Hover a word | Its meaning, and its base form (*heb → hebben*) |
| Click a word | Saved to your word list (turns **green**) |
| Right-click a word | Removed from the list |
| **+ EN** / **- EN** | Show or hide the original English |
| **Woorden** or **F10** | Open your word list |
| **x** | Hide the reader for this conversation |
| **F9** | Reader on/off |
| Mouse wheel over the reader | Scroll long texts |

Clicks on the reader never reach the game, so clicking a word doesn't skip the dialogue.

**Where meanings come from.** A built-in dictionary covers the most frequent words in the game: about 81% of all its
Dutch text in this version, growing with every update. Words that aren't in it yet are looked up on
[Wiktionary](https://en.wiktionary.org) while you play (internet needed) and remembered. You can turn that off in `config.json`.

## 3. Saved words → Anki

Each saved word keeps the Dutch word, its English meaning (with the base form), the sentence, and where you found it.

### Automatic (recommended)

1. In Anki: **Tools › Add-ons › Get Add-ons…**, code `2055492159` (AnkiConnect). Restart Anki.
2. Keep Anki open while you play. New words go to the **Speurtocht.NL** deck within a few seconds.

The note type is the same as Speurtocht.NL's (**Nederlands / English / Zin**, cards both ways). A word you already
saved in WoW is updated, not added twice. If Anki was closed, click **Naar Anki** in the word list later.

### By hand

In the word list: **Exporteer .txt** writes `Documents/PraatjeNL/praatje-woorden-<date>.txt`; **Kopieer** puts the same
text on the clipboard (on Linux this needs `xclip` or `wl-clipboard`). In Anki: **File › Import**, choose the file. Anki reads
the column names from the file; map them to Nederlands, English, Zin and Tags, with **Existing notes: Update**.

## 4. Settings

`Mods/PraatjeNL/config.json` appears after the first start.

| Setting | Default | |
|---|---|---|
| `ToggleReader` / `ToggleWordList` | `F9` / `F10` | Keys |
| `AutoOpenReader` | `true` | Open the reader with every dialogue or letter |
| `ShowEnglish` | `false` | English column on/off (also the **+ EN** button) |
| `FontScale`, `ReaderColumnWidth`, `ReaderMaxHeight` | `1`, `520`, `0.55` | Text size and reader size |
| `OnlineLookup` | `true` | Wiktionary for words not in the dictionary |
| `AnkiEnabled`, `AnkiUrl`, `AnkiDeck` | `true`, `http://127.0.0.1:8765`, `Speurtocht.NL` | Anki sync |
| `ExportFolder` | (Documents/PraatjeNL) | Where **Exporteer .txt** writes |

SMAPI console: `praatje status`, `praatje anki`, `praatje export`.

Your word list is stored in SMAPI's data folder (`.smapi/mod-data/emergencepathways.praatjenl/`), so updating the mod keeps it.

## 5. What it reads (0.1)

Villager dialogue, cutscenes and festival talk, answer choices, letters, Secret Notes and Journal Scraps.
Coming next: quests, the Help Wanted board, Special Orders, TV, item descriptions, and clicking words right inside the
dialogue box. See [ROADMAP.md](ROADMAP.md).

## Troubleshooting

- **Nothing is in Dutch:** choose Nederlands on page 2 of the language menu (title screen).
- **No reader:** press **F9**. Check the SMAPI window for red lines about Praatje.NL.
- **No English column:** some texts (generated lines, some events) have no single English source; the reader says so.
- **Words don't reach Anki:** Anki open? AnkiConnect installed? Then click **Naar Anki** in the word list; the message says what went wrong.

## For developers

Build: `dotnet build Praatje -c Release` (finds a Steam install; otherwise `-p:GamePath=...`). Tests:
`dotnet run --project tests/Praatje.Tests`. Dictionary work happens in batches; see [CLAUDE.md](CLAUDE.md).

The Dutch translation belongs to the StardewValley Dutch team (Spawk, JanFokke, Azelion). This repository contains no
game or translation text: only word frequencies and our own word meanings.
