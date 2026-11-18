"""Shared text helpers: clean Stardew dialogue codes and split Dutch text into words.

Keep these rules in step with Praatje/Text/WordSplitter.cs (the in-game version).
"""
import re

# A word: letters (incl. accented), with inner apostrophes or hyphens (auto's, zee-egel).
WORD_RE = re.compile(r"[A-Za-zÀ-ÖØ-öø-ÿ]+(?:['’\-][A-Za-zÀ-ÖØ-öø-ÿ]+)*")

_CODES = [
    (re.compile(r"%item[^%]*%%"), " "),                 # mail item attachments
    (re.compile(r"%(?:noun|adj|place|name|spouse|kid1|kid2|pet|farm|favorite|band|book|year|time|season|fork|revealtaste\S*)"), " "),
    (re.compile(r"\[#\].*$", re.S), " "),                # mail title after [#]
    (re.compile(r"\$\{([^}]*)\}\$"), lambda m: m.group(1).split("^")[0].split("¦")[0]),  # gender blocks: keep male form
    (re.compile(r"#?\$[a-z](?:\s*\d+)?#?"), " "),        # $h $s $b $e $k $q 0 ...
    (re.compile(r"\$\d+"), " "),                        # portrait numbers $9
    (re.compile(r"\{\{[^}]*\}\}"), " "),                 # tokens
    (re.compile(r"\[[^\]]*\]"), " "),                    # [LocalizedText ...] / item refs
    (re.compile(r"[@^#¦*]"), " "),
]


def clean(text: str) -> str:
    """Remove Stardew dialogue commands; keep readable Dutch."""
    s = str(text)
    for pat, rep in _CODES:
        s = pat.sub(rep, s)
    return re.sub(r"\s+", " ", s).strip()


def words(text: str):
    return WORD_RE.findall(text)


def norm(word: str) -> str:
    return word.replace("’", "'").strip("'-").lower()


_EVENT_TEXT = re.compile(r'(?:speak|message|textAboveHead|end dialogue|question|quickQuestion|splitSpeak|showText)\b[^"]*"([^"]*)"')


def strings_in(path_key: str, value):
    """Yield the human-readable strings inside one asset entry."""
    if isinstance(value, dict):
        for v in value.values():
            yield from strings_in(path_key, v)
    elif isinstance(value, list):
        for v in value:
            yield from strings_in(path_key, v)
    elif isinstance(value, str):
        if "Events/" in path_key or "Festivals/" in path_key:
            found = _EVENT_TEXT.findall(value)
            if found:
                yield from found
                return
            if "/" in value and '"' not in value:
                return          # pure event script, no text
        if "Data/Quests" in path_key or "Bundles" in path_key or "NPCGiftTastes" in path_key or "Boots" in path_key or "hats" in path_key or "Monsters" in path_key or "Achievements" in path_key:
            # slash-separated records: keep fields that contain spaces or letters only
            for part in value.split("/"):
                if re.search(r"[A-Za-zÀ-ÿ]{2,}", part) and not re.fullmatch(r"[\w\s\(\)\.\-]*\d[\w\s\(\)\.\-]*", part):
                    yield part
            return
        yield value
