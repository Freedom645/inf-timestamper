from enum import StrEnum

from domain.entity.sdvx_game_entity import PlayData
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.entity.timestamp_formatter import AbstractGameTimestampFormatter, TimestampExtractorMixin


class SDVXFormatID(StrEnum):
    TIMESTAMP = "timestamp"
    TITLE = "title"
    LEVEL = "level"
    DIFFICULTY = "difficulty"
    SCORE = "score"
    EX_SCORE = "ex_score"
    CLEAR_LAMP = "clear_lamp"

    def __str__(self) -> str:
        return self.value

    def logical_name(self) -> str:
        return SDVX_FORMAT_ID_LOGICAL_NAMES.get(self, self.value)


SDVX_FORMAT_ID_LOGICAL_NAMES = {
    SDVXFormatID.TIMESTAMP: "タイムスタンプ",
    SDVXFormatID.TITLE: "タイトル",
    SDVXFormatID.LEVEL: "レベル",
    SDVXFormatID.DIFFICULTY: "難易度",
    SDVXFormatID.CLEAR_LAMP: "クリアランプ",
    SDVXFormatID.SCORE: "スコア",
    SDVXFormatID.EX_SCORE: "EXスコア",
}


class SDVXGameTimestampFormatter(TimestampExtractorMixin, AbstractGameTimestampFormatter[PlayData, SDVXFormatID]):
    def format_ids(self) -> list[SDVXFormatID]:
        return list(SDVXFormatID)

    def extract_value(
        self,
        identifier: SDVXFormatID,
        session: StreamSession[PlayData],
        timestamp: Timestamp[PlayData],
    ) -> str:
        if identifier is SDVXFormatID.TIMESTAMP:
            return self.extract_timestamp(session, timestamp)

        if cd := timestamp.data.chart_detail:
            match identifier:
                case SDVXFormatID.TITLE:
                    return cd.title
                case SDVXFormatID.LEVEL:
                    return str(cd.level)
                case SDVXFormatID.DIFFICULTY:
                    return cd.difficulty
                case _:
                    pass

        if pr := timestamp.data.play_result:
            match identifier:
                case SDVXFormatID.CLEAR_LAMP:
                    return pr.clear_lamp.name
                case SDVXFormatID.EX_SCORE:
                    return str(pr.ex_score)
                case SDVXFormatID.SCORE:
                    return str(pr.score)
                case _:
                    pass
        return ""
