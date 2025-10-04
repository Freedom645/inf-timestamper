from pydantic import BaseModel

from domain.entity.stream_entity import TimestampData
from domain.value.game_value import DJ_LEVEL, ClearLamp


class PlayResult(BaseModel):
    dj_level: DJ_LEVEL = DJ_LEVEL.F
    """DJ LEVEL (AAA ~ F)"""
    lamp: ClearLamp = ClearLamp.NO_PLAY
    """クリアランプ"""
    gauge: str = ""
    """ゲージオプション"""
    p_great: int = 0
    """P-GREAT"""
    great: int = 0
    """GREAT"""
    good: int = 0
    """GOOD"""
    bad: int = 0
    """BAD"""
    poor: int = 0
    """POOR"""
    fast: int = 0
    """FAST"""
    slow: int = 0
    """SLOW"""
    combo_break: int = 0
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
    title: str = ""
    """楽曲名"""
    level: int = -1
    """レベル"""
    artist: str = ""
    """アーティスト名"""
    genre: str = ""
    """ジャンル"""
    bpm: str = ""
    """BPM"""
    min_bpm: str = ""
    """最小BPM"""
    max_bpm: str = ""
    """最大BPM"""
    difficulty: str = ""
    """難易度"""
    note_count: int = 0
    """ノーツ数"""


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
