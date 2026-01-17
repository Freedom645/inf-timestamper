from pydantic import BaseModel, Field
from pathlib import Path

from domain.entity.inf_game_format import InfFormatID
from domain.entity.sdvx_game_format import SDVXFormatID
from domain.value.stream_value import StreamKind


class SettingObs(BaseModel):
    is_enabled: bool = False
    """OBS Studio連携を有効にする"""
    host: str = "localhost"
    """OBS Studioのホスト"""
    port: int = 4455
    """OBS Studioのポート"""
    password: str = ""
    """OBS Studioのパスワード"""


class SettingReflux(BaseModel):
    directory: Path = Path()


class SettingYoutube(BaseModel):
    auth_type: str = ""


class SettingTimestampFormat(BaseModel):
    include_start_label: bool = True
    """開始ラベルを含める"""
    start_label: str = "00:00 配信開始"
    """開始ラベルのテキスト"""

    template: str = (
        f"${InfFormatID.TIMESTAMP} ${InfFormatID.TITLE} [Lv.${InfFormatID.LEVEL}] "
        f"(DJ LEVEL: ${InfFormatID.DJ_LEVEL}, EXスコア: ${InfFormatID.EX_SCORE}, ランプ: ${InfFormatID.CLEAR_LAMP})"
    )
    """タイムスタンプの表示フォーマット"""


class SettingSdvx(BaseModel):
    sdvx_helper_directory: Path = Path()
    """SDVX Helperのディレクトリ"""
    include_start_label: bool = True
    """開始ラベルを含める"""
    start_label: str = "00:00 配信開始"
    """開始ラベルのテキスト"""
    template: str = f"${SDVXFormatID.TIMESTAMP} ${SDVXFormatID.TITLE} [Lv.${SDVXFormatID.LEVEL}] ${SDVXFormatID.SCORE} (${SDVXFormatID.CLEAR_LAMP})"
    """タイムスタンプの表示フォーマット"""


class Settings(BaseModel):
    obs: SettingObs = Field(default_factory=SettingObs)
    reflux: SettingReflux = Field(default_factory=SettingReflux)
    youtube: SettingYoutube = Field(default_factory=SettingYoutube)
    timestamp: SettingTimestampFormat = Field(default_factory=SettingTimestampFormat)
    sdvx: SettingSdvx = Field(default_factory=SettingSdvx)
    stream_kind: StreamKind = StreamKind.INF
    """配信の種類"""

    def reset_settings(self) -> "Settings":
        self.obs = SettingObs()
        self.reflux = SettingReflux()
        self.youtube = SettingYoutube()
        self.timestamp = SettingTimestampFormat()
        self.sdvx = SettingSdvx()
        self.stream_kind = StreamKind.INF
        return self

    def bind_settings(self, other: "Settings") -> "Settings":
        self.obs = other.obs
        self.reflux = other.reflux
        self.youtube = other.youtube
        self.timestamp = other.timestamp
        self.sdvx = other.sdvx
        self.stream_kind = other.stream_kind
        return self
