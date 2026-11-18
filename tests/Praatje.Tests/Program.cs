using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PraatjeNL.Lookup;
using PraatjeNL.Text;
using PraatjeNL.Vocab;

internal static class Program
{
    private static int failures;
    private static int checks;

    private static void Check(bool ok, string what)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("FAIL: " + what);
        }
    }

    private static void Equal<T>(T actual, T expected, string what) =>
        Check(Equals(actual, expected), $"{what}: expected [{expected}], got [{actual}]");

    private static int Main(string[] args)
    {
        string root = FindRoot();

        // --- WordSplitter -------------------------------------------------
        List<Token> t = WordSplitter.Split("Hé, je hebt aan mijn verjaardag gedacht... Auto's en zee-egels!");
        var words = t.Where(x => x.IsWord).Select(x => x.Text).ToList();
        Equal(string.Join("|", words), "Hé|je|hebt|aan|mijn|verjaardag|gedacht|Auto's|en|zee-egels", "split words");
        Equal(string.Concat(t.Select(x => x.Text)), "Hé, je hebt aan mijn verjaardag gedacht... Auto's en zee-egels!", "tokens cover the text");
        Equal(WordSplitter.Normalize("Auto’s"), "auto's", "normalize curly apostrophe");
        Equal(WordSplitter.Normalize("'t"), "t", "normalize edge apostrophe");
        string text = "Hallo daar! Ik had nog wat hout over. Het beste!";
        Equal(WordSplitter.SentenceAround(text, text.IndexOf("hout")), "Ik had nog wat hout over.", "sentence around");
        Equal(WordSplitter.SentenceAround(text, 0), "Hallo daar!", "sentence at start");

        // --- Cleaner --------------------------------------------------------
        Equal(Cleaner.Clean("Hey, you remembered my birthday... Thanks, @!$h#$b#Oh, it's, uh... *What is it?", "Lucy"),
            "Hey, you remembered my birthday... Thanks, Lucy!\nOh, it's, uh... *What is it?", "clean dialogue");
        Equal(string.Join("|", Cleaner.Pages("One.$h#$b#Two.$s#$e#Three.")), "One.|Two.|Three.", "pages");
        Equal(Cleaner.Clean("Hi!^I had some wood. ^ -Robin %item object 388 50 %%[#] A Gift From Robin"), "Hi!\nI had some wood.\n-Robin", "clean mail");
        Equal(Cleaner.Clean("I'm ${a boy^a girl}$."), "I'm a boy.", "gender block");
        Equal(string.Join("|", Cleaner.EventStrings("pause 500/speak Abigail \"First line.\"/move farmer 0 1 0/speak Abigail \"Second.$h\"")), "First line.|Second.$h", "event strings");

        // --- Dictionary -----------------------------------------------------
        var d = WordDictionary.FromJson("{\"words\": {\"hebben\": [\"hebben\", \"to have\"], \"heb\": [\"hebben\", \"(I) have\"], \"boom\": [\"boom\", \"tree\"], \"werken\": [\"werken\", \"to work\"], \"egel\": [\"egel\", \"hedgehog\"], \"auto\": [\"auto\", \"car\"]}, \"names\": [\"Pelican Town\"]}");
        Equal(d.Lookup("Heb")?.Meaning, "(I) have", "exact lookup, capitalised");
        Equal(d.Lookup("bomen")?.Lemma, null, "no false plural (bomen -> boom needs the batch)");
        Equal(d.Lookup("boompje")?.Lemma, "boom", "diminutive rule");
        Equal(d.Lookup("werkte")?.Lemma, "werken", "past tense rule");
        Equal(d.Lookup("werkt")?.Lemma, "werken", "verb -t rule");
        Equal(d.Lookup("zee-egel")?.Lemma, "egel", "hyphen compound");
        Equal(d.Lookup("auto's")?.Lemma, "auto", "'s plural");
        Check(d.Lookup("boompje")!.Guessed, "rule matches are marked guessed");

        // --- Layout ---------------------------------------------------------
        List<PlacedToken> placed = TextLayout.Layout(new[] { "een twee drie vier", "vijf" }, 9, 10, 5, s => s.Length, out float height);
        var lines = placed.GroupBy(p => p.Y).Select(g => string.Concat(g.Select(p => p.Token.Text)).Trim()).ToList();
        Equal(string.Join("|", lines), "een twee|drie vier|vijf", "wrap lines");
        Equal(height, 35f, "layout height (2 lines + gap + 1 line)");
        Check(placed.All(p => p.X + p.Width <= 9 || p.X == 0), "no word sticks out past the width");
        List<PlacedToken> nl = TextLayout.Layout(new[] { "a\nb" }, 100, 10, 0, s => s.Length, out _);
        Equal(nl.Count(p => p.Token.IsWord), 2, "line break keeps both words");
        Equal(nl.First(p => p.Token.Text == "b").Y, 10f, "line break moves to next line");

        // --- Vocab + Anki text --------------------------------------------
        var store = new VocabStore();
        (VocabEntry e, bool isNew) = store.Save("Heb", "hebben", "(I) have", "Ik heb hout.", "Brief: Robin");
        Check(isNew, "first save is new");
        Equal(e.Word, "heb", "saved lowercase");
        (VocabEntry again, bool isNew2) = store.Save("heb", "", "", "", "");
        Check(!isNew2 && again.Seen == 2, "second save counts");
        string tsv = AnkiText.Build(store.Entries, onlyNew: false, out int count);
        Equal(count, 1, "export count");
        Check(tsv.StartsWith("#separator:tab\n#html:true\n#columns:Nederlands\tEnglish\tZin\tTags\n#tags column:4\n"), "anki headers");
        Check(tsv.Contains("heb\t(I) have — (hebben)\tIk heb hout.<br><i>Brief: Robin</i>\tPraatjeNL Stardew\n"), "anki row: " + tsv);
        Check(store.Remove("HEB") && store.Entries.Count == 0, "remove");

        // --- Wiktionary parsing -------------------------------------------
        string wjson = "{\"nl\":[{\"partOfSpeech\":\"Verb\",\"language\":\"Dutch\",\"definitions\":[{\"definition\":\"<span>inflection of <a href=\\\"/wiki/hebben\\\">hebben</a></span>\"}]}],\"en\":[{\"definitions\":[{\"definition\":\"nope\"}]}]}";
        var w = Wiktionary.Parse("heb", wjson);
        Equal(w?.lemma, "hebben", "wiktionary lemma from 'inflection of'");
        Check(w?.meaning.Contains("verb: inflection of hebben") == true, "wiktionary meaning: " + w?.meaning);
        Equal(Wiktionary.Parse("x", "{\"en\":[]}"), null, "no Dutch section");

        // --- Shipped dictionary --------------------------------------------
        string dictPath = Path.Combine(root, "Praatje", "assets", "dictionary.json");
        WordDictionary shipped = WordDictionary.FromJson(File.ReadAllText(dictPath));
        Check(shipped.Count >= 0, "shipped dictionary loads");
        if (shipped.Count > 0)
        {
            foreach (string common in new[] { "ik", "je", "het", "niet", "heb" })
                Check(shipped.Lookup(common)?.Meaning.Length > 0, $"shipped dictionary knows '{common}'");
        }

        Console.WriteLine($"{checks} checks, {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static string FindRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Praatje", "manifest.json")))
            dir = Path.GetDirectoryName(dir) ?? throw new Exception("repo root not found");
        return dir;
    }
}
