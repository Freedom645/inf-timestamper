from injector import inject

from domain.entity.stream_entity import StreamSession
from domain.entity.settings_entity import Settings
from domain.repository.current_stream_session_repository import (
    CurrentStreamSessionRepository,
)


class InMemoryCurrentStreamSessionRepository(CurrentStreamSessionRepository):
    @inject
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._session = StreamSession(kind=settings.basic.stream_kind)

    def get(self) -> StreamSession:
        return self._session

    def set(self, stream_session: StreamSession) -> None:
        self._session = stream_session

    def reset(self) -> StreamSession:
        self._session = StreamSession(kind=self._settings.basic.stream_kind)
        return self._session
