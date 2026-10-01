"""Give the local Obsidian MCP connection its vault key without storing it in TOML."""

import json
import sys
from pathlib import Path


def main() -> int:
    project_root = Path(__file__).resolve().parents[2]
    settings = project_root / "Docs" / ".obsidian" / "plugins" / "obsidian-local-rest-api" / "data.json"
    if not settings.is_file():
        print("Absolute Zero Docs vault plugin settings are unavailable", file=sys.stderr)
        return 1

    try:
        key = json.loads(settings.read_text(encoding="utf-8")).get("apiKey", "")
    except (OSError, json.JSONDecodeError):
        print("Could not read the Obsidian plugin settings", file=sys.stderr)
        return 1

    if not isinstance(key, str) or not key:
        print("Obsidian plugin API key is missing", file=sys.stderr)
        return 1

    print(json.dumps({"Authorization": f"Bearer {key}"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
