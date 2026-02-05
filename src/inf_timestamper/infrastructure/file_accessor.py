import json
import logging
import xml.etree.ElementTree as ET

from pathlib import Path
from typing import TypeVar, Any
from injector import inject

T = TypeVar("T")

ENCODING = "utf-8"


class FileAccessor:
    @inject
    def __init__(self, logger: logging.Logger) -> None:
        self._logger = logger

    def load_as_json(self, path: Path) -> Any | None:
        if not path.exists():
            return None

        with open(path, "r", encoding=ENCODING) as f:
            return json.load(f)

    def save_as_json(self, path: Path, data: Any) -> None:
        with open(path, "w", encoding=ENCODING) as f:
            json.dump(data, f, ensure_ascii=False, default=lambda e: str(e))

    def load_as_text(self, path: Path, default: str | T = "") -> str | T:
        if not path.exists():
            return default

        with open(path, "r", encoding=ENCODING) as f:
            return f.read()

    def load_as_integer(self, path: Path, default: int | T = -1) -> int | T:
        if not path.exists():
            return default

        with open(path, "r", encoding=ENCODING) as f:
            try:
                return int(f.read().strip())
            except ValueError:
                return default

    def save_as_text(self, path: Path, data: str) -> None:
        with open(path, "w", encoding=ENCODING) as f:
            f.write(data)

    def load_as_xml(self, path: Path) -> ET.ElementTree | None:
        if not path.exists() or not path.is_file():
            return None

        try:
            return ET.parse(path)  # type: ignore
        except ET.ParseError as e:
            self._logger.error(f"XMLファイルの読み込みに失敗しました {path}: {e}")
            return None
