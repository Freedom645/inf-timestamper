from pydantic import BaseModel

from domain.entity.stream import TimestampData
from domain.value.game import DJ_LEVEL, ClearLamp


class PlayResult(BaseModel):
    dj_level: DJ_LEVEL
    """DJ LEVEL (AAA ~ F)"""
    lamp: ClearLamp
    """クリアランプ"""
    p_great: int
    """P-GREAT"""
    great: int
    """GREAT"""
    good: int
    """GOOD"""
    bad: int
    """BAD"""
    poor: int
    """POOR"""
    fast: int
    """FAST"""
    slow: int
    """SLOW"""
    combo_break: int
    """COMBO BREAK"""

    @property
    def ex_score(self) -> int:
        """EX SCORE"""
        return self.p_great * 2 + self.great

    @property
    def miss_count(self) -> int:
        """ミスカウント (BP)"""
        return self.bad + self.poor

    @property
    def miss_poor(self) -> int:
        """見逃しPOOR"""
        return self.combo_break - self.bad

    @property
    def empty_poor(self) -> int:
        """空POOR"""
        return self.miss_count - self.combo_break


class ChartDetail(BaseModel):
    artist: str
    """アーティスト名"""
    genre: str
    """ジャンル"""
    bpm: int
    """BPM"""
    difficulty: str
    """難易度"""
    note_count: int
    """ノーツ数"""


class PlayData(TimestampData):
    title: str
    """楽曲名"""
    level: int
    """レベル"""
    chart_detail: ChartDetail | None = None
    """譜面情報"""
    play_result: PlayResult | None = None
    """プレイ結果"""

    def equals_without_result(self, other: "PlayData") -> bool:
        """プレイ結果を除いた同一性を判定する"""
        return self.title == other.title and self.level == other.level
