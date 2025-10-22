from injector import inject

from domain.entity.settings_entity import Settings
from domain.repository.settings_repository import SettingsRepository, ChangedCallback


class SettingsUseCase:
    @inject
    def __init__(self, settings: Settings, settings_repository: SettingsRepository) -> None:
        self._settings = settings
        self._settings_repository = settings_repository

    def load_settings(self) -> Settings:
        loaded_settings = self._settings_repository.load()
        self._settings.bind_settings(loaded_settings)

        return self._settings

    def save_settings(self, settings: Settings) -> None:
        self._settings.bind_settings(settings)
        self._settings_repository.save(settings)

    def subscribe_to_changes(self, callback: ChangedCallback) -> None:
        self._settings_repository.subscribe(callback)
