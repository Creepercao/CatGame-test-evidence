"""Stage the F1 game delta in the existing pure-game checkout; no push or release."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / "Build/PublishSource"
BASELINE = "a47d72c"


def main():
    changed = subprocess.check_output(
        ["git", "diff", BASELINE, "HEAD", "--name-only"], cwd=ROOT, text=True
    ).splitlines()
    staged = []
    for name in changed:
        if not (name.startswith("Assets/") or name == "ProjectSettings/ProjectSettings.asset"):
            continue
        if (name.startswith("Assets/Tests/") or name.startswith("Assets/Scripts/Player")
                or name.startswith("Assets/Editor/F1ArtAudit") or name == "Assets/Editor/CatGameSetup.cs"):
            continue
        source = ROOT / name
        if not source.is_file():
            raise RuntimeError("Review source deletion explicitly: " + name)
        raw = source.read_bytes()
        result = raw
        transform = "identity"
        if name == "Assets/Scripts/CatActor.cs":
            text = raw.decode("utf-8-sig")
            text, count = re.subn(
                r"^#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR\r?\n"
                r"[ \t]*if \(PlayerVisualQA.IsScriptedFlow\) return;\r?\n#endif\r?\n", "", text, flags=re.M)
            assert count == 1, "Expected exactly one private QA guard"
            result = text.encode("utf-8")
            transform = "remove private Development QA guard"
        if source.suffix in (".cs", ".meta", ".asset", ".unity", ".asmdef", ".json"):
            normalized = result.replace(b"\r\n", b"\n")
            if normalized != result:
                transform = ("normalize LF" if transform == "identity" else transform + "; normalize LF")
            result = normalized
        dest = TARGET / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(result)
        staged.append({"path": name, "transform": transform,
                       "rootSha256": hashlib.sha256(raw).hexdigest(),
                       "gameSha256": hashlib.sha256(result).hexdigest()})

    # Keep the public build entry free of private QA builders; change just the game import operation.
    path = TARGET / "Assets/Editor/CatGameSetup.cs"
    text = path.read_text(encoding="utf-8-sig")
    text, count = re.subn(r"public static void ForceReimportArt\(\)\s*\{.*?\n        \}",
                         "public static void ForceReimportArt()\n        {\n            WorldArtMigration.Apply();\n        }",
                         text, count=1, flags=re.S)
    assert count == 1
    path.write_text(text, encoding="utf-8")
    output = ROOT / "Build/F1/pure-source-staging.json"
    output.write_text(json.dumps({"rootSource": subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(), "files": staged,
        "special": "Public CatGameSetup retains its release-only builders; import method now preserves metadata"},
        ensure_ascii=False, indent=2), encoding="utf-8")
    print("Staged", len(staged), "game files and preserved public release builders")


if __name__ == "__main__":
    main()
