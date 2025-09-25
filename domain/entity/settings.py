from enum import StrEnum
from pydantic import BaseModel, Field
from pathlib import Path

from domain.entity.game_format import FormatID


class SettingObs(BaseModel):
    host: str = "localhost"
    """OBS Studioのホスト"""
    port: int = 4444
    """OBS Studioのポート"""
    password: str = ""
    """OBS Studioのパスワード"""


class SettingReflux(BaseModel):
    directory: Path = Path()


class SettingYoutube(BaseModel):
    auth_type: str = ""


class SettingTimestampFormat(BaseModel):
    template: str = (
        f"${FormatID.TIMESTAMP} ${FormatID.TITLE} [Lv.${FormatID.LEVEL}] "
        f"(DJ LEVEL: ${FormatID.DJ_LEVEL}, EXスコア: ${FormatID.EX_SCORE}, ランプ: ${FormatID.CLEAR_LAMP})"
    )
    """タイムスタンプの表示フォーマット"""


class Settings(BaseModel):
    obs: SettingObs = Field(default_factory=SettingObs)
    reflux: SettingReflux = Field(default_factory=SettingReflux)
    youtube: SettingYoutube = Field(default_factory=SettingYoutube)
    timestamp: SettingTimestampFormat = Field(default_factory=SettingTimestampFormat)

    def reset_settings(self) -> "Settings":
        self.obs = SettingObs()
        self.reflux = SettingReflux()
        self.youtube = SettingYoutube()
        self.timestamp = SettingTimestampFormat()
        return self

    def bind_settings(self, other: "Settings") -> "Settings":
        self.obs = other.obs
        self.reflux = other.reflux
        self.youtube = other.youtube
        self.timestamp = other.timestamp
        return self
