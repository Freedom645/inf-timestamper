from enum import StrEnum
from string import Template

from domain.entity.game_entity import PlayData
from domain.entity.stream_entity import StreamSession, Timestamp


class FormatID(StrEnum):
    TIMESTAMP = "timestamp"
    TITLE = "title"
    LEVEL = "level"
    ARTIST = "artist"
    GENRE = "genre"
    BPM = "bpm"
    DIFFICULTY = "difficulty"
    NOTE_COUNT = "note_count"
    DJ_LEVEL = "dj_level"
    CLEAR_LAMP = "clear_lamp"
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
    FormatID.TIMESTAMP: "タイムスタンプ",
    FormatID.TITLE: "タイトル",
    FormatID.LEVEL: "レベル",
    FormatID.ARTIST: "アーティスト",
    FormatID.GENRE: "ジャンル",
    FormatID.BPM: "BPM",
    FormatID.DIFFICULTY: "難易度",
    FormatID.NOTE_COUNT: "ノーツ数",
    FormatID.DJ_LEVEL: "DJ LEVEL",
    FormatID.CLEAR_LAMP: "クリアランプ",
    FormatID.EX_SCORE: "EXスコア",
    FormatID.MISS_COUNT: "ミスカウント",
    FormatID.MISS_POOR: "見逃しPOOR",
    FormatID.EMPTY_POOR: "空POOR",
    FormatID.P_GREAT: "P-GREAT",
    FormatID.GREAT: "GREAT",
    FormatID.GOOD: "GOOD",
    FormatID.BAD: "BAD",
    FormatID.POOR: "POOR",
    FormatID.FAST: "FAST",
    FormatID.SLOW: "SLOW",
    FormatID.COMBO_BREAK: "COMBO BREAK",
}


class GameTimestampFormatter:
    def __init__(
        self, format_str: str, default_value: dict[FormatID, str] = {}
    ) -> None:
        self.template = Template(format_str)
        self.default_value = default_value

    def format(
        self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]
    ) -> str:
        mapping: dict[str, str] = {}
        for identifier in FormatID:
            try:
                mapping[identifier.value] = self._extract_value(
                    identifier, session, timestamp
                )
            except Exception:
                # FIXME: 握り潰しちゃっているので検知方法など考えたい
                mapping[identifier.value] = self.default_value.get(identifier, "")

        return self.template.safe_substitute(mapping)

    def _extract_value(
        self,
        identifier: FormatID,
        session: StreamSession[PlayData],
        timestamp: Timestamp[PlayData],
    ) -> str:
        match identifier:
            case FormatID.TIMESTAMP:
                return str(session.get_elapse(timestamp))
            case FormatID.TITLE:
                return timestamp.data.title
            case FormatID.LEVEL:
                return str(timestamp.data.level)
            case _:
                pass

        if timestamp.data.chart_detail:
            match identifier:
                case FormatID.ARTIST:
                    return timestamp.data.chart_detail.artist
                case FormatID.GENRE:
                    return timestamp.data.chart_detail.genre
                case FormatID.BPM:
                    return str(timestamp.data.chart_detail.bpm)
                case FormatID.DIFFICULTY:
                    return timestamp.data.chart_detail.difficulty
                case FormatID.NOTE_COUNT:
                    return str(timestamp.data.chart_detail.note_count)
                case _:
                    pass

        if timestamp.data.play_result:
            match identifier:
                case FormatID.DJ_LEVEL:
                    return timestamp.data.play_result.dj_level.name
                case FormatID.CLEAR_LAMP:
                    return timestamp.data.play_result.lamp.name
                case FormatID.EX_SCORE:
                    return str(timestamp.data.play_result.ex_score)
                case FormatID.MISS_COUNT:
                    return str(timestamp.data.play_result.miss_count)
                case FormatID.MISS_POOR:
                    return str(timestamp.data.play_result.miss_poor)
                case FormatID.EMPTY_POOR:
                    return str(timestamp.data.play_result.empty_poor)
                case FormatID.P_GREAT:
                    return str(timestamp.data.play_result.p_great)
                case FormatID.GREAT:
                    return str(timestamp.data.play_result.great)
                case FormatID.GOOD:
                    return str(timestamp.data.play_result.good)
                case FormatID.BAD:
                    return str(timestamp.data.play_result.bad)
                case FormatID.POOR:
                    return str(timestamp.data.play_result.poor)
                case FormatID.FAST:
                    return str(timestamp.data.play_result.fast)
                case FormatID.SLOW:
                    return str(timestamp.data.play_result.slow)
                case FormatID.COMBO_BREAK:
                    return str(timestamp.data.play_result.combo_break)
                case _:
                    pass
        return ""
