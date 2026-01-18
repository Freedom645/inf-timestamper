from typing import Protocol

from domain.value.stream_value import StreamKind
from domain.entity.timestamp_formatter import GameTimestampFormatter


class GameTimestampFormatterFactory(Protocol):
    def __call__(self, kind: StreamKind) -> GameTimestampFormatter | None: ...
