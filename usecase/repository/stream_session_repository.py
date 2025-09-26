from abc import ABC, abstractmethod
from pathlib import Path
from typing import Generic, TypeVar

from domain.entity.stream import StreamSession, TimestampData

T = TypeVar("T", bound=TimestampData)


class StreamSessionRepository(ABC, Generic[T]):
    @abstractmethod
    def load(self, path: Path) -> StreamSession[T] | None: ...

    @abstractmethod
    def save(self, stream_session: StreamSession[T]) -> None: ...
