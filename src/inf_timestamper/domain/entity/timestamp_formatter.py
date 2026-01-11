from __future__ import annotations
from abc import ABC, abstractmethod
from string import Template
from typing import TypeVar, Generic
from enum import StrEnum

from domain.entity.stream_entity import StreamSession, Timestamp, TimestampData

TPlayData = TypeVar("TPlayData", bound=TimestampData)
TFormatID = TypeVar("TFormatID", bound=StrEnum)


class AbstractGameTimestampFormatter(Generic[TPlayData, TFormatID], ABC):
    def __init__(
        self,
        format_str: str,
        default_value: dict[TFormatID, str] | None = None,
    ) -> None:
        self.template = Template(format_str)
        self.default_value = default_value or {}

    def format(
        self,
        session: StreamSession[TPlayData],
        timestamp: Timestamp[TPlayData],
    ) -> str:
        mapping: dict[str, str] = {}

        for identifier in self.format_ids():
            try:
                mapping[identifier.value] = self.extract_value(identifier, session, timestamp)
            except Exception:
                mapping[identifier.value] = self.default_value.get(identifier, "")

        return self.template.safe_substitute(mapping)

    @abstractmethod
    def format_ids(self) -> list[TFormatID]:
        """使用する FormatID 一覧"""
        raise NotImplementedError

    @abstractmethod
    def extract_value(
        self,
        identifier: TFormatID,
        session: StreamSession[TPlayData],
        timestamp: Timestamp[TPlayData],
    ) -> str:
        """identifier から値を取り出す"""
        raise NotImplementedError


class TimestampExtractorMixin:
    def extract_timestamp(
        self,
        session: StreamSession[TPlayData],
        timestamp: Timestamp[TPlayData],
    ) -> str:
        if session.start_time is None:
            return timestamp.occurred_at.strftime("%Y/%m/%d %H:%M:%S")
        return str(timestamp.get_elapse(session.start_time))
