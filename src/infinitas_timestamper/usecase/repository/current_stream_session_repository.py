from abc import ABC, abstractmethod
from typing import TypeVar, Generic

from domain.entity.stream_entity import StreamSession, TimestampData

T = TypeVar("T", bound=TimestampData)


class CurrentStreamSessionRepository(ABC, Generic[T]):
    @abstractmethod
    def get(self) -> StreamSession[T] | None: ...

    @abstractmethod
    def set(self, stream_session: StreamSession[T]) -> None: ...

    @abstractmethod
    def clear(self) -> None: ...
