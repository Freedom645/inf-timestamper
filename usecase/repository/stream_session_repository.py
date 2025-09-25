from abc import ABC, abstractmethod
from pathlib import Path

from domain.entity.stream import StreamSession


class StreamSessionRepository(ABC):
    @abstractmethod
    def load(self, path: Path) -> StreamSession: ...

    @abstractmethod
    def save(self, stream_session: StreamSession) -> None: ...
