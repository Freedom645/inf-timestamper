from domain.entity.game_entity import PlayData
from domain.entity.stream_entity import StreamSession
from usecase.repository.current_stream_session_repository import (
    CurrentStreamSessionRepository,
)


class InMemoryCurrentStreamSessionRepository(CurrentStreamSessionRepository[PlayData]):
    def __init__(self):
        self._session: StreamSession[PlayData] | None = None

    def get(self) -> StreamSession[PlayData] | None:
        return self._session

    def set(self, stream_session: StreamSession[PlayData]) -> None:
        self._session = stream_session

    def clear(self) -> None:
        self._session = None
