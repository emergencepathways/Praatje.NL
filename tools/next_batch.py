#!/usr/bin/env python3
"""Prepare the next dictionary batch: the most frequent words that have no meaning yet.

Usage:
    python3 tools/next_batch.py            # next free batch number
    python3 tools/next_batch.py NN         # (re)make batch NN's work file
    python3 tools/next_batch.py --size 400 # batch size (default 500)

Reads source/wordlist.json (word frequencies) and batches/*.json (words already done).
Writes work/batch_NN.txt (the words, with how often they occur and an example sentence if
work/examples.json exists) and work/batch_NN.list.json (the exact word list the importer checks).
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_SIZE = 500


def done_words(exclude_batch=None):
    done = set()
    for path in glob.glob(os.path.join(ROOT, "batches", "*.json")):
        m = re.match(r"(\d+)", os.path.basename(path))
        if exclude_batch is not None and m and int(m.group(1)) == exclude_batch:
            continue
        b = json.load(open(path, encoding="utf-8"))
        done.update(b.get("words", {}).keys())
        done.update(w.lower() for w in b.get("names", []))
        done.update(b.get("skip", []))
    return done


def batch_numbers():
    nums = []
    for path in glob.glob(os.path.join(ROOT, "batches", "*.json")):
        m = re.match(r"(\d+)", os.path.basename(path))
        if m:
            nums.append(int(m.group(1)))
    return nums


def main():
    args = sys.argv[1:]
    size = DEFAULT_SIZE
    if "--size" in args:
        i = args.index("--size")
        size = int(args[i + 1])
        del args[i:i + 2]
    nums = batch_numbers()
    nn = int(args[0]) if args else (max(nums) + 1 if nums else 1)

    wordlist = json.load(open(os.path.join(ROOT, "source", "wordlist.json"), encoding="utf-8"))
    done = done_words(exclude_batch=nn)
    todo = [w for w in wordlist if w["w"] not in done][:size]
    if not todo:
        print("ALL DONE: every word in source/wordlist.json has a meaning.")
        return
    ex_path = os.path.join(ROOT, "work", "examples.json")
    examples = json.load(open(ex_path, encoding="utf-8")) if os.path.exists(ex_path) else {}

    os.makedirs(os.path.join(ROOT, "work"), exist_ok=True)
    total = sum(w["n"] for w in wordlist)
    covered_after = sum(w["n"] for w in wordlist if w["w"] in done or w in todo)
    lines = [
        "# Batch %02d: %d words (frequency %d..%d)" % (nn, len(todo), todo[0]["n"], todo[-1]["n"]),
        "# After this batch the dictionary covers %.1f%% of all Dutch text in the game." % (100.0 * covered_after / total),
        "# word<TAB>count<TAB>note<TAB>example",
        "",
    ]
    for w in todo:
        note = "name?" if w.get("cap", 0) >= 0.9 else ""
        lines.append("%s\t%d\t%s\t%s" % (w["w"], w["n"], note, examples.get(w["w"], "")))
    with open(os.path.join(ROOT, "work", "batch_%02d.txt" % nn), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    json.dump([w["w"] for w in todo], open(os.path.join(ROOT, "work", "batch_%02d.list.json" % nn), "w", encoding="utf-8"),
              ensure_ascii=False)
    print("%02d" % nn)
    print("work/batch_%02d.txt: %d words; coverage after: %.1f%%" % (nn, len(todo), 100.0 * covered_after / total))


if __name__ == "__main__":
    main()
