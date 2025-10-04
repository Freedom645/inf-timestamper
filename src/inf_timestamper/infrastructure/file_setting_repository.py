from injector import inject

from domain.value.base_path import BasePath
from domain.entity.settings_entity import Settings
from usecase.repository.settings_repository import SettingsRepository
from infrastructure.file_accessor import FileAccessor


class FileSettingsRepository(SettingsRepository):
    @inject
    def __init__(self, file_accessor: FileAccessor, base_path: BasePath):
        self._file_accessor = file_accessor
        self._file_path = base_path / "settings.json"

    def load(self) -> Settings:
        data = self._file_accessor.load_as_text(self._file_path, default=None)
        if data is None:
            return Settings()

        return Settings.model_validate_json(data)

    def save(self, setting: Settings) -> None:
        self._file_accessor.save_as_text(self._file_path, setting.model_dump_json(indent=2))
