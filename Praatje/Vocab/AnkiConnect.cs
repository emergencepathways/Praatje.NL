using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PraatjeNL.Vocab
{
    /// <summary>
    /// Sends words straight to Anki through the AnkiConnect add-on (code 2055492159) on 127.0.0.1:8765.
    /// Uses the same note type as Speurtocht.NL's anki_sync.py, so a word already learned in WoW
    /// is updated rather than added twice.
    /// </summary>
    public sealed class AnkiConnect
    {
        public const string Model = "Speurtocht.NL";
        private static readonly string[] Fields = { "Nederlands", "English", "Zin" };
        private static readonly string[] NoteTags = { "PraatjeNL", "Stardew" };

        private const string Css = @".card { font-family: ""Noto Sans"", Arial, sans-serif; font-size: 26px; text-align: center;
  color: #2b2118; background: #f6efe2; }
.nl { font-size: 34px; font-weight: 600; color: #7a3e12; }
.en { font-size: 26px; }
.zin { margin-top: 22px; font-size: 17px; color: #5b4a3a; line-height: 1.45; }
.zin i { color: #8a7560; }
.nightMode .card, .card.nightMode { color: #eadfcd; background: #2a241e; }
.nightMode .nl { color: #f0b37e; }
.nightMode .zin { color: #c9b9a4; }";

        private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(6) };
        private readonly string url;
        private readonly string deck;
        private bool setupDone;

        public AnkiConnect(string url, string deck)
        {
            this.url = url;
            this.deck = deck;
        }

        /// <summary>Add the word, or fill empty fields of an existing note. Returns "added", "updated" or "unchanged".</summary>
        public async Task<string> AddOrUpdateAsync(VocabEntry e)
        {
            await this.EnsureSetupAsync();
            var fields = new JsonObject
            {
                ["Nederlands"] = e.Word,
                ["English"] = AnkiText.EnglishField(e),
                ["Zin"] = AnkiText.ZinField(e),
            };

            JsonNode? ids = await this.CallAsync("findNotes", new JsonObject
            {
                ["query"] = $"\"note:{Model}\" \"Nederlands:{Escape(e.Word)}\"",
            });
            if (ids is JsonArray arr && arr.Count > 0)
            {
                long id = arr[0]!.GetValue<long>();
                JsonNode? info = await this.CallAsync("notesInfo", new JsonObject { ["notes"] = new JsonArray(id) });
                JsonNode? current = info?[0]?["fields"];
                var changes = new JsonObject();
                foreach (string f in new[] { "English", "Zin" })
                {
                    string have = current?[f]?["value"]?.GetValue<string>() ?? "";
                    string want = fields[f]!.GetValue<string>();
                    if (have.Length == 0 && want.Length > 0)
                        changes[f] = want;
                }
                if (changes.Count == 0)
                    return "unchanged";
                await this.CallAsync("updateNoteFields", new JsonObject { ["note"] = new JsonObject { ["id"] = id, ["fields"] = changes } });
                return "updated";
            }

            await this.CallAsync("addNote", new JsonObject
            {
                ["note"] = new JsonObject
                {
                    ["deckName"] = this.deck,
                    ["modelName"] = Model,
                    ["fields"] = fields,
                    ["tags"] = new JsonArray(NoteTags.Select(t => (JsonNode)JsonValue.Create(t)!).ToArray()),
                    ["options"] = new JsonObject { ["allowDuplicate"] = false, ["duplicateScope"] = "deck" },
                },
            });
            return "added";
        }

        private async Task EnsureSetupAsync()
        {
            if (this.setupDone)
                return;
            await this.CallAsync("createDeck", new JsonObject { ["deck"] = this.deck });
            JsonNode? models = await this.CallAsync("modelNames", null);
            bool exists = models is JsonArray list && list.Any(m => m?.GetValue<string>() == Model);
            if (!exists)
            {
                var templates = new JsonArray
                {
                    new JsonObject
                    {
                        ["Name"] = "NL → EN",
                        ["Front"] = "<div class=\"nl\">{{Nederlands}}</div>",
                        ["Back"] = "{{FrontSide}}<hr id=\"answer\"><div class=\"en\">{{English}}</div><div class=\"zin\">{{Zin}}</div>",
                    },
                    new JsonObject
                    {
                        ["Name"] = "EN → NL",
                        ["Front"] = "{{#English}}<div class=\"en\">{{English}}</div>{{/English}}",
                        ["Back"] = "{{FrontSide}}<hr id=\"answer\"><div class=\"nl\">{{Nederlands}}</div><div class=\"zin\">{{Zin}}</div>",
                    },
                };
                await this.CallAsync("createModel", new JsonObject
                {
                    ["modelName"] = Model,
                    ["inOrderFields"] = new JsonArray(Fields.Select(f => (JsonNode)JsonValue.Create(f)!).ToArray()),
                    ["css"] = Css,
                    ["isCloze"] = false,
                    ["cardTemplates"] = templates,
                });
            }
            else
            {
                JsonNode? have = await this.CallAsync("modelFieldNames", new JsonObject { ["modelName"] = Model });
                var names = (have as JsonArray)?.Select(n => n?.GetValue<string>()).ToList() ?? new List<string?>();
                string[] missing = Fields.Where(f => !names.Contains(f)).ToArray();
                if (missing.Length > 0)
                    throw new InvalidOperationException($"Anki note type '{Model}' has no field(s) {string.Join(", ", missing)}. Rename its fields to Nederlands / English / Zin.");
            }
            this.setupDone = true;
        }

        private async Task<JsonNode?> CallAsync(string action, JsonObject? parameters)
        {
            var body = new JsonObject { ["action"] = action, ["version"] = 6 };
            if (parameters != null)
                body["params"] = parameters;
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage resp = await this.http.PostAsync(this.url, content);
            string text = await resp.Content.ReadAsStringAsync();
            JsonNode? node = JsonNode.Parse(text);
            string? error = node?["error"]?.ToString();
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"AnkiConnect {action}: {error}");
            return node?["result"];
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("*", "\\*").Replace("_", "\\_");
    }
}
