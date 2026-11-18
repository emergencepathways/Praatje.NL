#!/usr/bin/env python3
"""Check a dictionary batch and rebuild Praatje/assets/dictionary.json from all batches.

Usage:
    python3 tools/import_batch.py NN     # check batch NN against work/batch_NN.list.json, then rebuild
    python3 tools/import_batch.py        # check every batch's format and rebuild (used by CI)

Batch file: batches/NN-<slug>.json
    {
      "batch": NN,
      "words": { "form": ["base form", "English meaning"], ... },
      "names": ["Name", ...],     # names of people/places/things: shown red, never saved
      "skip":  ["word", ...]      # not Dutch (English game keys, codes, sounds)
    }
Prints READY when the batch is complete and valid.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Praatje", "assets", "dictionary.json")
WORD = re.compile(r"^[a-zà-öø-ÿ]+(?:['\-][a-zà-öø-ÿ]+)*$")


def load_batches():
    batches = {}
    for path in sorted(glob.glob(os.path.join(ROOT, "batches", "*.json"))):
        m = re.match(r"(\d+)-[\w\-]+\.json$", os.path.basename(path))
        if not m:
            raise SystemExit("bad batch file name: %s (want NN-slug.json)" % os.path.basename(path))
        try:
            batches[int(m.group(1))] = (path, json.load(open(path, encoding="utf-8")))
        except json.JSONDecodeError as e:
            raise SystemExit("%s: invalid JSON: %s" % (os.path.basename(path), e))
    return batches


def check(nn, path, b, errors):
    name = os.path.basename(path)
    if b.get("batch") != nn:
        errors.append("%s: \"batch\" must be %d" % (name, nn))
    words = b.get("words", {})
    if not isinstance(words, dict):
        errors.append("%s: \"words\" must be an object" % name)
        return
    for form, v in words.items():
        if form != form.lower() or not WORD.match(form):
            errors.append("%s: word key %r must be one lowercase word" % (name, form))
        if not (isinstance(v, list) and len(v) == 2 and all(isinstance(x, str) for x in v)):
            errors.append("%s: %r must be [\"base form\", \"meaning\"]" % (name, form))
            continue
        lemma, meaning = v
        if not lemma.strip():
            errors.append("%s: %r has no base form" % (name, form))
        if not meaning.strip():
            errors.append("%s: %r has no meaning" % (name, form))
        if any(c in meaning for c in "\t\n<>"):
            errors.append("%s: %r meaning has a tab, newline or HTML" % (name, form))
        if len(meaning) > 120:
            errors.append("%s: %r meaning is longer than 120 characters" % (name, form))
    for k in ("names", "skip"):
        if not isinstance(b.get(k, []), list):
            errors.append("%s: \"%s\" must be a list" % (name, k))


def main():
    nn = int(sys.argv[1]) if len(sys.argv) > 1 else None
    batches = load_batches()
    errors = []
    for n, (path, b) in batches.items():
        check(n, path, b, errors)

    if nn is not None:
        if nn not in batches:
            raise SystemExit("no batches/%02d-*.json yet" % nn)
        list_path = os.path.join(ROOT, "work", "batch_%02d.list.json" % nn)
        if os.path.exists(list_path):
            want = json.load(open(list_path, encoding="utf-8"))
            b = batches[nn][1]
            have = set(b.get("words", {})) | {w.lower() for w in b.get("names", [])} | set(b.get("skip", []))
            missing = [w for w in want if w not in have]
            if missing:
                errors.append("batch %02d is missing %d word(s): %s" % (nn, len(missing), ", ".join(missing[:60])))
        else:
            print("(no work/batch_%02d.list.json: only the format was checked)" % nn)

    # Same form in two batches with different meanings: the later batch wins, but say so.
    merged, names, seen = {}, set(), {}
    for n in sorted(batches):
        b = batches[n][1]
        for form, v in b.get("words", {}).items():
            if form in merged and merged[form] != v:
                print("note: '%s' redefined in batch %02d (was batch %02d)" % (form, n, seen[form]))
            merged[form] = v
            seen[form] = n
        names.update(b.get("names", []))

    if errors:
        print("\n".join(errors))
        print("NOT READY: %d problem(s)" % len(errors))
        sys.exit(1)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    data = {"words": dict(sorted(merged.items())), "names": sorted(names, key=str.lower)}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")

    wl_path = os.path.join(ROOT, "source", "wordlist.json")
    if os.path.exists(wl_path):
        wl = json.load(open(wl_path, encoding="utf-8"))
        total = sum(w["n"] for w in wl)
        lower_names = {x.lower() for x in names}
        skip = set()
        for n in batches:
            skip.update(batches[n][1].get("skip", []))
        cov = sum(w["n"] for w in wl if w["w"] in merged or w["w"] in lower_names or w["w"] in skip)
        print("dictionary: %d word forms, %d names; covers %.1f%% of the game's Dutch text" % (len(merged), len(names), 100.0 * cov / total))
    print("READY")


if __name__ == "__main__":
    main()
