from enum import StrEnum
from typing import Callable
from packaging.version import Version
from pydantic import BaseModel, HttpUrl


class UpdateStep(StrEnum):
    BACKUP = "backup"
    DOWNLOAD = "download"
    APPLY_UPDATE = "apply_update"


ProgressCallback = Callable[[int], None]
StepProgressCallback = Callable[[UpdateStep, int], None]


class VersionInfo(BaseModel):
    class Config:
        frozen = True

    version_str: str
    asset_url: HttpUrl

    @property
    def version(self) -> Version:
        return Version(self.version_str)
