from pydantic import BaseModel

from domain.value.sdvx_game_value import ClearLamp
from domain.entity.stream_entity import TimestampData


class ChartDetail(BaseModel):
    title: str = ""
    """楽曲名"""
    level: int = -1
    """レベル"""
    difficulty: str = ""
    """難易度"""


class PlayResult(BaseModel):
    score: int = 0
    """スコア"""
    ex_score: int = 0
    """EX SCORE"""
    clear_lamp: ClearLamp = ClearLamp.FAILED
    """クリアランプ"""


class PlayData(TimestampData):
    key: str
    """一意なキー"""
    chart_detail: ChartDetail | None = None
    """譜面情報"""
    play_result: PlayResult | None = None
    """プレイ結果"""

    def equals_without_result(self, other: "PlayData") -> bool:
        """プレイ結果を除いた同一性を判定する"""
        return self.key == other.key
