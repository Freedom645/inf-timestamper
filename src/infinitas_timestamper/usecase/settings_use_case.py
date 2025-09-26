from injector import inject

from domain.entity.settings_entity import Settings
from usecase.repository.settings_repository import SettingsRepository


class SettingsUseCase:

    @inject
    def __init__(
        self, settings: Settings, settings_repository: SettingsRepository
    ) -> None:
        self._settings = settings
        self._settings_repository = settings_repository

    def load_settings(self) -> Settings:
        loaded_settings = self._settings_repository.load()
        self._settings.bind_settings(loaded_settings)

        return self._settings

    def save_settings(self, settings: Settings) -> None:
        self._settings_repository.save(settings)
        self._settings.bind_settings(settings)
