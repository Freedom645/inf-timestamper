from domain.entity.stream_entity import StreamSession
from domain.repository.current_stream_session_repository import (
    CurrentStreamSessionRepository,
)


class InMemoryCurrentStreamSessionRepository(CurrentStreamSessionRepository):
    def __init__(self) -> None:
        self._session = StreamSession()

    def get(self) -> StreamSession:
        return self._session

    def set(self, stream_session: StreamSession) -> None:
        self._session = stream_session

    def reset(self) -> StreamSession:
        self._session = StreamSession()
        return self._session
