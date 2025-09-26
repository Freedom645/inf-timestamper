from typing import Protocol

from domain.entity.stream_entity import StreamSession, Timestamp
from domain.entity.game_entity import PlayData


class PlayRecordingPresenter(Protocol):
    def stream_started(self, session: StreamSession[PlayData]) -> None: ...
    def stream_ended(self, session: StreamSession[PlayData]) -> None: ...
    def timestamp_added(
        self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]
    ) -> None: ...
    def timestamp_updated(
        self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]
    ) -> None: ...
