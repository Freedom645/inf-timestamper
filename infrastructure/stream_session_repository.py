from pathlib import Path
from injector import inject
from datetime import datetime

from domain.entity.game import PlayData
from domain.entity.stream import StreamSession
from domain.value.base_path import BasePath
from usecase.repository.stream_session_repository import StreamSessionRepository
from infrastructure.file_accessor import FileAccessor


class FileStreamSessionRepository(StreamSessionRepository[PlayData]):

    @inject
    def __init__(self, file_accessor: FileAccessor, base_path: BasePath):
        self._file_accessor = file_accessor
        self._sessions_path = base_path / "sessions"

    def save(self, stream_session: StreamSession[PlayData]) -> None:
        if not self._sessions_path.exists():
            self._sessions_path.mkdir(exist_ok=True)

        file_date = stream_session.start_time or datetime.now()
        file_name = file_date.strftime("%Y-%m-%d_%H-%M-%S.json")

        file_path = self._sessions_path / file_name
        json_str = stream_session.model_dump_json()

        self._file_accessor.save_as_text(file_path, json_str)

    def load(self, path: Path) -> StreamSession[PlayData] | None:
        json_str = self._file_accessor.load_as_text(path)
        if not json_str:
            return None

        return StreamSession[PlayData].model_validate_json(json_str)
