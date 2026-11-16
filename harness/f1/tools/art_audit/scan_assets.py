"""Read-only source-art baseline; candidate roots are not proof of runtime use."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path

from PIL import Image


def scan(project):
    rows = []
    for path in sorted((project / "Assets/Resources/Art").rglob("*.png")):
        relative = path.relative_to(project).as_posix()
        meta_path = path.with_suffix(path.suffix + ".meta")
        raw_meta = meta_path.read_bytes() if meta_path.exists() else b""
        meta = raw_meta.decode("utf-8-sig")

        def scalar(name):
            match = re.search(r"^[ \t]*" + re.escape(name) + r":[ \t]*([^\r\n]*)", meta, re.M)
            return match.group(1).strip() if match else None

        kind = "legacy-or-other"
        for marker, category in (("/Cats/Pixel64/", "pixel-cat"), ("/Room/Pixel/", "pixel-room"),
                                 ("/Items/Pixel/", "pixel-item"), ("/UI/Pixel/", "pixel-ui"),
                                 ("/Story/", "illustration")):
            if marker in relative:
                kind = category
                break
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            row = {"path": relative, "category": kind, "width": image.width, "height": image.height,
                   "pngSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                   "metaSha256": hashlib.sha256(raw_meta).hexdigest(),
                   "guid": scalar("guid"), "ppu": scalar("spritePixelsToUnits"),
                   "pivot": scalar("spritePivot"), "alignment": scalar("alignment"),
                   "filterMode": scalar("filterMode"), "mipmaps": scalar("enableMipMap"),
                   "userData": scalar("userData"), "spriteMode": scalar("spriteMode"),
                   "alphaBoundsTopLeft": alpha.getbbox()}
            if kind == "pixel-cat" and image.height == 64 and image.width % 64 == 0:
                row["frameAlphaBoundsTopLeft"] = [alpha.crop((x, 0, x + 64, 64)).getbbox()
                                                    for x in range(0, image.width, 64)]
            rows.append(row)
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=project, text=True).strip()
    return {"sourceCommit": revision, "scope": "All source Art PNGs; candidate categories require runtime confirmation",
            "pixelMeaning": "Source texels and frame canvas, not guaranteed screen pixels",
            "counts": dict(Counter(r["category"] for r in rows)), "assets": rows}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit("Refusing to overwrite an existing baseline")
    report = scan(args.project.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"sourceCommit": report["sourceCommit"], "counts": report["counts"]}))
