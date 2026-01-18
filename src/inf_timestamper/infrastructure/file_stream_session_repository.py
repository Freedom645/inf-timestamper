from pathlib import Path
from injector import inject
from datetime import datetime

from domain.entity.stream_entity import StreamSession
from domain.value.base_path import BasePath
from domain.repository.stream_session_repository import StreamSessionRepository
from infrastructure.mapper.timestamp_mapper import StreamSessionMapper
from infrastructure.dto.stream_session_dto import StreamSessionDTO
from infrastructure.file_accessor import FileAccessor


class FileStreamSessionRepository(StreamSessionRepository):
    @inject
    def __init__(self, file_accessor: FileAccessor, base_path: BasePath):
        self._file_accessor = file_accessor
        self._sessions_path = base_path / "sessions"
        self._mapper = StreamSessionMapper()

    def save(self, stream_session: StreamSession) -> None:
        if not self._sessions_path.exists():
            self._sessions_path.mkdir(exist_ok=True)

        file_date = stream_session.start_time or datetime.now()
        file_name = stream_session.kind.value + "_" + file_date.strftime("%Y-%m-%d_%H-%M-%S.json")

        file_path = self._sessions_path / file_name
        json_str = self._mapper.from_domain(stream_session).model_dump_json()

        self._file_accessor.save_as_text(file_path, json_str)

        old_file_name = file_date.strftime("%Y-%m-%d_%H-%M-%S.json")
        old_file_path = self._sessions_path / old_file_name
        if old_file_path.exists():
            old_file_path.unlink()

    def load(self, path: Path) -> StreamSession | None:
        json_dict = self._file_accessor.load_as_json(path)
        if not json_dict:
            return None

        dto = StreamSessionDTO.model_validate(json_dict)
        return self._mapper.to_domain(dto)
