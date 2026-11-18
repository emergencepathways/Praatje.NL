#!/usr/bin/env python3
"""Count every Dutch word in the StardewValley Dutch mod's text.

Usage:
    python3 tools/extract_words.py <path to StardewValleyDutch/assets>

Writes source/wordlist.json: [{"w": word, "n": count, "cap": share capitalised}], most frequent first,
and work/examples.json (one example sentence per word, for batch context; not committed: it is the
Dutch mod's text, which belongs to its authors).
"""
import json
import os
import re
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from textutil import clean, words, norm, strings_in  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SENT = re.compile(r"[^.!?…]*[.!?…]+|[^.!?…]+$")


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    base = sys.argv[1]
    counts, caps, examples, where = Counter(), Counter(), {}, defaultdict(set)
    for dirpath, _, files in os.walk(base):
        for f in sorted(files):
            if not f.endswith(".json"):
                continue
            path = os.path.join(dirpath, f)
            key = os.path.relpath(path, base).replace(os.sep, "/")
            try:
                data = json.load(open(path, encoding="utf-8-sig"))
            except json.JSONDecodeError as e:
                print("skip %s: %s" % (key, e))
                continue
            for raw in strings_in(key, data):
                text = clean(raw)
                for sent in SENT.findall(text):
                    sent = sent.strip()
                    for i, w in enumerate(words(sent)):
                        k = norm(w)
                        if not k:
                            continue
                        counts[k] += 1
                        if w[0].isupper() and i > 0:
                            caps[k] += 1
                        where[k].add(key.split("/")[0])
                        if k not in examples or (len(sent) < len(examples[k]) and len(sent) > 25):
                            examples[k] = sent[:220]
    out = [{"w": w, "n": n, "cap": round(caps[w] / n, 2)} for w, n in counts.most_common()]
    os.makedirs(os.path.join(ROOT, "source"), exist_ok=True)
    os.makedirs(os.path.join(ROOT, "work"), exist_ok=True)
    json.dump(out, open(os.path.join(ROOT, "source", "wordlist.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=0)
    json.dump(examples, open(os.path.join(ROOT, "work", "examples.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=0)
    total = sum(counts.values())
    print("%d words of text, %d different words" % (total, len(counts)))


if __name__ == "__main__":
    main()
