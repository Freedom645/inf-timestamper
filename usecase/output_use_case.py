from pathlib import Path
from injector import inject
import pyperclip

from domain.entity.game import PlayData
from domain.entity.stream import StreamSession
from usecase.repository.stream_session_repository import StreamSessionRepository


class OutputUseCase:

    @inject
    def __init__(self, stream_session_repository: StreamSessionRepository):
        self._stream_session_repository = stream_session_repository

    def copy_to_clipboard(self, stream_session: StreamSession[PlayData]) -> None:
        if stream_session.start_time is None:
            raise ValueError("配信が開始していません。")

        lines: list[str] = []
        for delta, timestamp in stream_session.get_timestamp_list():
            line = f"{delta} {timestamp.data.title} [Lv.{timestamp.data.level}]"
            lines.append(line)

        pyperclip.copy("\n".join(lines))

    def save_stream_session(self, stream_session: StreamSession[PlayData]) -> None:
        self._stream_session_repository.save(stream_session)

    def load_stream_session(self, file_path: Path) -> StreamSession[PlayData]:
        return self._stream_session_repository.load(file_path)
