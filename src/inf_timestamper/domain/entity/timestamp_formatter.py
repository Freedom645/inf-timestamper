from enum import StrEnum
from abc import abstractmethod
from string import Template
from typing import Protocol, TypeVar, Generic

from domain.entity.stream_entity import StreamSession, Timestamp

TFormatID = TypeVar("TFormatID", bound=StrEnum)


class GameTimestampFormatter(Protocol):
    def format(self, session: StreamSession, timestamp: Timestamp) -> str: ...


class GameTimestampFormatterBase(GameTimestampFormatter, Generic[TFormatID]):
    def __init__(
        self,
        format_str: str,
        default_value: dict[TFormatID, str] | None = None,
    ) -> None:
        self.template = Template(format_str)
        self.default_value = default_value or {}

    @abstractmethod
    def format_ids(self) -> list[TFormatID]: ...

    @abstractmethod
    def extract_value(
        self,
        identifier: TFormatID,
        session: StreamSession,
        timestamp: Timestamp,
    ) -> str: ...

    def format(self, session: StreamSession, timestamp: Timestamp) -> str:
        mapping: dict[str, str] = {}

        for identifier in self.format_ids():
            try:
                mapping[identifier.value] = self.extract_value(identifier, session, timestamp)
            except Exception:
                mapping[identifier.value] = self.default_value.get(identifier, "")

        return self.template.safe_substitute(mapping)


class TimestampExtractorMixin:
    def extract_timestamp(self, session: StreamSession, timestamp: Timestamp) -> str:
        if session.start_time is None:
            return timestamp.occurred_at.strftime("%Y/%m/%d %H:%M:%S")
        return str(timestamp.get_elapse(session.start_time))
