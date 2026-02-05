from pydantic import BaseModel

from domain.value.sdvx_game_value import SDVXClearLamp
from domain.entity.stream_entity import TimestampData


class SDVXChartDetail(BaseModel):
    title: str = ""
    """楽曲名"""
    level: int = -1
    """レベル"""
    difficulty: str = ""
    """難易度"""


class SDVXPlayResult(BaseModel):
    score: int = 0
    """スコア"""
    ex_score: int = 0
    """EX SCORE"""
    clear_lamp: SDVXClearLamp = SDVXClearLamp.FAILED
    """クリアランプ"""


class SDVXPlayData(TimestampData):
    key: str
    """一意なキー"""
    chart_detail: SDVXChartDetail | None = None
    """譜面情報"""
    play_result: SDVXPlayResult | None = None
    """プレイ結果"""

    def equals_without_result(self, other: TimestampData) -> bool:
        """プレイ結果を除いた同一性を判定する"""
        if not isinstance(other, SDVXPlayData):
            return False
        return self.key == other.key
