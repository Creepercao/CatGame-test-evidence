"""Check photographed room edges for the camera's clear-color strip."""

import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image


def inspect(path):
    with Image.open(path) as image:
        frame = image.convert("RGB")
    clear = (143, 184, 158)
    widths = []
    for ratio in (.35, .5, .75):
        y = round(frame.height * ratio)
        width = 0
        for x in range(frame.width - 1, -1, -1):
            if max(abs(a - b) for a, b in zip(frame.getpixel((x, y)), clear)) > 2:
                break
            width += 1
        widths.append(width)
    return {"imageSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "width": frame.width, "height": frame.height, "rightClearWidths": widths,
            "covered": max(widths) <= 2}


def room_right_edge_should_be_covered(image_path):
    path = Path(image_path)
    result = inspect(path)
    path.with_suffix(".coverage.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    assert result["covered"], result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = inspect(args.image)
    args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))
    raise SystemExit(0 if result["covered"] else 1)
