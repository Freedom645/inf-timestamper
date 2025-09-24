import json
from pathlib import Path

ENCODING = "utf-8"


class FileAccessor:

    def load_as_json(self, path: Path) -> dict | None:
        if not path.exists():
            return None

        with open(path, "r", encoding=ENCODING) as f:
            return json.load(f)

    def save_as_json(self, path: Path, data: dict):
        with open(path, "w", encoding=ENCODING) as f:
            json.dump(data, f, ensure_ascii=False, default=lambda e: str(e))

    def load_as_text(self, path: Path, default: str | None = None) -> str | None:
        if not path.exists():
            return default

        with open(path, "r", encoding=ENCODING) as f:
            return f.read()

    def load_as_integer(self, path: Path, default: int = -1) -> int:
        if not path.exists():
            return default

        with open(path, "r", encoding=ENCODING) as f:
            try:
                return int(f.read().strip())
            except ValueError:
                return default

    def save_as_text(self, path: Path, data: str):
        with open(path, "w", encoding=ENCODING) as f:
            f.write(data)
