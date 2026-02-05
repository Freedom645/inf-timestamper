from abc import ABC, abstractmethod
from pathlib import Path

from domain.entity.stream_entity import StreamSession


class StreamSessionRepository(ABC):
    @abstractmethod
    def load(self, path: Path) -> StreamSession | None: ...

    @abstractmethod
    def save(self, stream_session: StreamSession) -> None: ...
