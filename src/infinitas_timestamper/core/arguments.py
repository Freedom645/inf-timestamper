import sys
import argparse

from enum import StrEnum
from pydantic import BaseModel


class ArgUpdateResult(StrEnum):
    SUCCESS = "success"
    FAILED = "failed"


class Arguments(BaseModel):
    class Config:
        frozen = True

    update_result: ArgUpdateResult | None = None

    @staticmethod
    def load() -> "Arguments":
        parser = argparse.ArgumentParser(description="Infinitas Timestamper")
        parser.add_argument("--update-result", type=ArgUpdateResult, choices=list(ArgUpdateResult))

        args = parser.parse_args(sys.argv[1:])

        return Arguments(**vars(args))
