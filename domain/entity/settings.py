from pydantic import BaseModel, Field
from pathlib import Path


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


class Settings(BaseModel):
    obs: SettingObs = Field(default_factory=SettingObs)
    reflux: SettingReflux = Field(default_factory=SettingReflux)
    youtube: SettingYoutube = Field(default_factory=SettingYoutube)

    def reset_settings(self) -> "Settings":
        self.obs = SettingObs()
        self.reflux = SettingReflux()
        self.youtube = SettingYoutube()
        return self

    def bind_settings(self, other: "Settings") -> "Settings":
        self.obs = other.obs
        self.reflux = other.reflux
        self.youtube = other.youtube
        return self
