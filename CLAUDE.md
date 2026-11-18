# Praatje.NL — instructions for Claude sessions

SMAPI mod for Stardew Valley (folder `PraatjeNL`, UniqueID `EmergencePathways.PraatjeNL`) that makes the Dutch text on
screen clickable for language learners: a reader panel shows the current dialogue/letter with every word clickable,
an optional English column, a saved word list, straight-to-Anki sync and a .txt export. Sister project of Speurtocht.NL
(`emergencepathways/NLQuest`, the WoW addon); it shares that project's Anki note type and deck.
Owner: Emergence Pathways. The owner is learning Dutch (A1–A2) and plays Stardew natively on Linux through Steam.

The Dutch text itself comes from the **StardewValley Dutch** mod (JanFokke, Nexus 24290), which the player installs
separately. **Never commit that mod's text** (no sentences, no asset files): it belongs to its authors. Committed data is
word-level only: `source/wordlist.json` (word frequencies) and `batches/*.json` (our meanings).

## The standing task: "next batch" (dictionary meanings)

Each batch gives English meanings to the next 500 most frequent words that don't have one yet.

```bash
git switch main && git pull
python3 tools/next_batch.py                 # prints NN, writes work/batch_NN.txt + work/batch_NN.list.json
git switch -c batch-NN
# write batches/NN-<slug>.json (format below) for EVERY word in work/batch_NN.txt
python3 tools/import_batch.py NN            # repeat until READY; rebuilds Praatje/assets/dictionary.json
dotnet run --project tests/Praatje.Tests    # must end with "0 failures" (add -p:RestoreConfigFile=... offline, see Building)
# add one line under "Unreleased" in CHANGELOG.md: "Batch NN: <n> words, coverage X% -> Y%"
git add -A && git commit                    # subject "Batch NN: <n> words (coverage Y%)"
git push -u origin batch-NN                 # then open a PR to main (or push to main if the owner says so)
```

`work/` is git-ignored. If `work/examples.json` exists (made by `tools/extract_words.py`, needs the Dutch mod's files),
the batch file shows an example sentence per word; without it, write meanings from the word alone.

### Batch format

```json
{
  "batch": 3,
  "words": { "bomen": ["boom", "trees"], "heb": ["hebben", "(I) have"] },
  "names": ["Pelican", "Abigail"],
  "skip":  ["true", "speak"]
}
```

- `words`: key = the exact lowercase form from the batch file; value = `[base form, short English meaning]`.
  Meanings: short, learner-friendly, the sense used in Stardew first, `;` between senses, max 120 characters,
  no HTML/tabs. Verb forms say the person when it helps: `"(I) have"`, `"has"`, `"had"`.
- `names`: people, places, brands and invented words (shown red in game, never saved). Keep the capital letter.
  A word that is also ordinary Dutch (`jas` = coat, `kent` = knows) goes in `words`; it may also be in `names`.
- `skip`: not Dutch at all (English internal keys, script commands, stray letters).
- Words marked `name?` in the batch file are usually names, but check.

## Building the mod

The mod compiles against the player's own game files (`Praatje/Praatje.csproj`, property `GamePath`; Linux Steam paths
are found automatically). Normal machines: `dotnet build Praatje -c Release` with a .NET 6+ SDK.

Claude sessions have no NuGet access and no game. Recipe (ask the owner for folder access to their Stardew Valley folder
through the device tools, then stage these files from it):

- `Stardew Valley.dll`, `StardewModdingAPI.dll`, `MonoGame.Framework.dll`, `StardewValley.GameData.dll`, `xTile.dll`,
  `smapi-internal/SMAPI.Toolkit.CoreInterfaces.dll` → `/home/claude/refs/` (keep `smapi-internal/`)
- every `System*.dll`, `netstandard.dll`, `mscorlib.dll` from the game folder → `/home/claude/rt/`
- `apt-get install -y dotnet-sdk-8.0`, and an offline NuGet config:
  `printf '<configuration><packageSources><clear /></packageSources></configuration>' > /home/claude/offline-nuget.config`

```bash
OFF="-p:OfflineRuntime=true -p:GamePath=/home/claude/refs -p:RuntimeRefPath=/home/claude/rt -p:RestoreConfigFile=/home/claude/offline-nuget.config"
dotnet build Praatje -c Release $OFF
dotnet run --project tests/Praatje.Tests -p:RestoreConfigFile=/home/claude/offline-nuget.config
python3 tools/package.py $OFF               # -> dist/PraatjeNL-<version>.zip (one PraatjeNL/ folder)
```

To look up game internals without a decompiler: `dotnet run --project tools/apidump -p:RestoreConfigFile=... -- "/home/claude/refs/Stardew Valley.dll" StardewValley.Menus.DialogueBox` (types, fields, methods; a trailing `*` matches a namespace).

## Layout

- `Praatje/ModEntry.cs`: wiring (events, input, word saving, Anki queue, export, console command `praatje`).
- `Praatje/Capture/`: `TextCapture` reads the Dutch text from `DialogueBox` / `LetterViewerMenu`;
  `EnglishSource` reads the original English from the game's content files (bypassing SMAPI and the Dutch mod),
  by `Dialogue.TranslationKey`, mail key, event script position, or by matching the Dutch entry.
- `Praatje/UI/`: `ReaderPanel` (clickable text, +EN column, drag to move), `WordListPanel` ("Mijn woorden").
- `Praatje/Text/`: game-independent text code (word splitting, cleaning, layout, dictionary). Keep `WordSplitter` and
  `Cleaner` in step with `tools/textutil.py`.
- `Praatje/Vocab/`: saved words, Anki text (same columns as Speurtocht.NL), AnkiConnect client.
- `Praatje/Lookup/Wiktionary.cs`: online fallback (en.wiktionary REST definitions, Dutch section), cached.
- `tests/Praatje.Tests`: offline checks for `Text/`, `Vocab/` and Wiktionary parsing. No game needed.
- `tools/`: `extract_words.py` (word frequencies from the Dutch mod), `next_batch.py`, `import_batch.py`, `package.py`.

## Rules

- UI text is Dutch with English in tooltips/hints (like Speurtocht.NL). Keep it short and A2-friendly.
- Saved words and the Wiktionary cache live in SMAPI global data (`.smapi/mod-data/emergencepathways.praatjenl/`),
  so they survive mod updates. Don't move them into the mod folder.
- Releases: bump `Version` in `Praatje/manifest.json` and `Praatje/Praatje.csproj`, move CHANGELOG "Unreleased" to a
  version heading, build with `tools/package.py`, and attach the zip to a GitHub release (`gh release create vX.Y.Z dist/*.zip`)
  or send it to the owner.
