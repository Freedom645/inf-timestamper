from enum import StrEnum

from domain.entity.inf_game_entity import InfPlayData
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.entity.timestamp_formatter import AbstractGameTimestampFormatter, TimestampExtractorMixin


class InfFormatID(StrEnum):
    TIMESTAMP = "timestamp"
    TITLE = "title"
    LEVEL = "level"
    ARTIST = "artist"
    GENRE = "genre"
    BPM = "bpm"
    MIN_BPM = "min_bpm"
    MAX_BPM = "max_bpm"
    DIFFICULTY = "difficulty"
    NOTE_COUNT = "note_count"
    DJ_LEVEL = "dj_level"
    CLEAR_LAMP = "clear_lamp"
    GAUGE = "gauge"
    EX_SCORE = "ex_score"
    MISS_COUNT = "miss_count"
    MISS_POOR = "miss_poor"
    EMPTY_POOR = "empty_poor"
    P_GREAT = "p_great"
    GREAT = "great"
    GOOD = "good"
    BAD = "bad"
    POOR = "poor"
    FAST = "fast"
    SLOW = "slow"
    COMBO_BREAK = "combo_break"

    def __str__(self) -> str:
        return self.value

    def logical_name(self) -> str:
        return FORMAT_ID_LOGICAL_NAMES.get(self, self.value)


FORMAT_ID_LOGICAL_NAMES = {
    InfFormatID.TIMESTAMP: "タイムスタンプ",
    InfFormatID.TITLE: "タイトル",
    InfFormatID.LEVEL: "レベル",
    InfFormatID.ARTIST: "アーティスト",
    InfFormatID.GENRE: "ジャンル",
    InfFormatID.BPM: "BPM",
    InfFormatID.MIN_BPM: "最小BPM",
    InfFormatID.MAX_BPM: "最大BPM",
    InfFormatID.DIFFICULTY: "難易度",
    InfFormatID.NOTE_COUNT: "ノーツ数",
    InfFormatID.DJ_LEVEL: "DJ LEVEL",
    InfFormatID.CLEAR_LAMP: "クリアランプ",
    InfFormatID.GAUGE: "ゲージ",
    InfFormatID.EX_SCORE: "EXスコア",
    InfFormatID.MISS_COUNT: "ミスカウント",
    InfFormatID.MISS_POOR: "見逃しPOOR",
    InfFormatID.EMPTY_POOR: "空POOR",
    InfFormatID.P_GREAT: "P-GREAT",
    InfFormatID.GREAT: "GREAT",
    InfFormatID.GOOD: "GOOD",
    InfFormatID.BAD: "BAD",
    InfFormatID.POOR: "POOR",
    InfFormatID.FAST: "FAST",
    InfFormatID.SLOW: "SLOW",
    InfFormatID.COMBO_BREAK: "COMBO BREAK",
}


class InfGameTimestampFormatter(TimestampExtractorMixin, AbstractGameTimestampFormatter[InfPlayData, InfFormatID]):
    def format_ids(self) -> list[InfFormatID]:
        return list(InfFormatID)

    def extract_value(
        self,
        identifier: InfFormatID,
        session: StreamSession[InfPlayData],
        timestamp: Timestamp[InfPlayData],
    ) -> str:
        if identifier is InfFormatID.TIMESTAMP:
            return self.extract_timestamp(session, timestamp)

        if cd := timestamp.data.chart_detail:
            match identifier:
                case InfFormatID.TITLE:
                    return cd.title
                case InfFormatID.LEVEL:
                    return str(cd.level)
                case InfFormatID.ARTIST:
                    return cd.artist
                case InfFormatID.GENRE:
                    return cd.genre
                case InfFormatID.BPM:
                    return cd.bpm
                case InfFormatID.MIN_BPM:
                    return cd.min_bpm
                case InfFormatID.MAX_BPM:
                    return cd.max_bpm
                case InfFormatID.DIFFICULTY:
                    return cd.difficulty
                case InfFormatID.NOTE_COUNT:
                    return str(cd.note_count)
                case _:
                    pass

        if pr := timestamp.data.play_result:
            match identifier:
                case InfFormatID.DJ_LEVEL:
                    return pr.dj_level.name
                case InfFormatID.CLEAR_LAMP:
                    return pr.lamp.name
                case InfFormatID.EX_SCORE:
                    return str(pr.ex_score)
                case InfFormatID.MISS_COUNT:
                    return str(pr.miss_count)
                case InfFormatID.MISS_POOR:
                    return str(pr.miss_poor)
                case InfFormatID.EMPTY_POOR:
                    return str(pr.empty_poor)
                case InfFormatID.GAUGE:
                    return pr.gauge
                case InfFormatID.P_GREAT:
                    return str(pr.p_great)
                case InfFormatID.GREAT:
                    return str(pr.great)
                case InfFormatID.GOOD:
                    return str(pr.good)
                case InfFormatID.BAD:
                    return str(pr.bad)
                case InfFormatID.POOR:
                    return str(pr.poor)
                case InfFormatID.FAST:
                    return str(pr.fast)
                case InfFormatID.SLOW:
                    return str(pr.slow)
                case InfFormatID.COMBO_BREAK:
                    return str(pr.combo_break)
                case _:
                    pass
        return ""
