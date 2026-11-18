#!/usr/bin/env python3
"""Build the player download: dist/PraatjeNL-<version>.zip with one PraatjeNL/ folder.

Usage:
    python3 tools/package.py [extra dotnet build arguments...]

Builds Praatje in Release (against your game folder: see Praatje/Praatje.csproj), then zips
PraatjeNL.dll, manifest.json and assets/. Unzip it into Stardew Valley/Mods.
"""
import json
import os
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main():
    subprocess.run(["python3", os.path.join(ROOT, "tools", "import_batch.py")], check=True)
    subprocess.run(["dotnet", "build", os.path.join(ROOT, "Praatje"), "-c", "Release", "-o", os.path.join(ROOT, "Praatje", "bin", "pack")] + sys.argv[1:], check=True)
    manifest = json.load(open(os.path.join(ROOT, "Praatje", "manifest.json"), encoding="utf-8-sig"))
    version = manifest["Version"]
    out_dir = os.path.join(ROOT, "dist")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "PraatjeNL-%s.zip" % version)
    pack = os.path.join(ROOT, "Praatje", "bin", "pack")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(os.path.join(pack, "PraatjeNL.dll"), "PraatjeNL/PraatjeNL.dll")
        if os.path.exists(os.path.join(pack, "PraatjeNL.pdb")):
            z.write(os.path.join(pack, "PraatjeNL.pdb"), "PraatjeNL/PraatjeNL.pdb")
        z.write(os.path.join(ROOT, "Praatje", "manifest.json"), "PraatjeNL/manifest.json")
        assets = os.path.join(ROOT, "Praatje", "assets")
        for name in sorted(os.listdir(assets)):
            z.write(os.path.join(assets, name), "PraatjeNL/assets/" + name)
    print(out)


if __name__ == "__main__":
    main()
