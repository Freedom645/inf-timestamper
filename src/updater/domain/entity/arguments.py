import sys
import argparse

from enum import StrEnum
from pathlib import Path
from pydantic import BaseModel


class Mode(StrEnum):
    CHECK = "check"
    UPDATE = "update"


class Arguments(BaseModel):
    mode: Mode
    update_path: Path | None = None

    class Config:
        frozen = True

    @staticmethod
    def load_from_sysargs() -> "Arguments":
        parser = argparse.ArgumentParser(description="Infinitas Timestamper updater")

        group = parser.add_mutually_exclusive_group(required=True)
        group.add_argument("--check", action="store_true", help="最新バージョンをチェックする")
        group.add_argument("--update", type=str, metavar="PATH", help="指定フォルダに最新バージョンを展開する")

        args = parser.parse_args(sys.argv[1:])

        if args.check:
            return Arguments(mode=Mode.CHECK)

        if args.update:
            return Arguments(mode=Mode.UPDATE, update_path=Path(args.update).resolve())

        raise ValueError("Either --check or --update must be specified")
