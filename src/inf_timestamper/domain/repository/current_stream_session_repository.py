from abc import ABC, abstractmethod

from domain.entity.stream_entity import StreamSession


class CurrentStreamSessionRepository(ABC):
    @abstractmethod
    def get(self) -> StreamSession: ...

    @abstractmethod
    def set(self, stream_session: StreamSession) -> None: ...

    @abstractmethod
    def reset(self) -> StreamSession: ...
