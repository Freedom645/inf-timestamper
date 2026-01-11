from domain.entity.inf_game_entity import InfPlayData
from domain.entity.stream_entity import StreamSession
from domain.repository.current_stream_session_repository import (
    CurrentStreamSessionRepository,
)


class InMemoryCurrentStreamSessionRepository(CurrentStreamSessionRepository[InfPlayData]):
    def __init__(self) -> None:
        self._session = StreamSession[InfPlayData]()

    def get(self) -> StreamSession[InfPlayData]:
        return self._session

    def set(self, stream_session: StreamSession[InfPlayData]) -> None:
        self._session = stream_session

    def reset(self) -> StreamSession[InfPlayData]:
        self._session = StreamSession[InfPlayData]()
        return self._session
