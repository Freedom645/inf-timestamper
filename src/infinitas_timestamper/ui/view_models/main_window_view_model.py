from injector import inject
from PySide6.QtCore import QObject

from domain.entity.settings_entity import Settings
from usecase.settings_use_case import SettingsUseCase


class MainWindowViewModel(QObject):

    @inject
    def __init__(self, settings_use_case: SettingsUseCase) -> None:
        QObject.__init__(self)
        self._settings_use_case = settings_use_case

    def get_settings(self) -> Settings:
        return self._settings_use_case.load_settings()

    def update_setting(self, settings: Settings) -> None:
        self._settings_use_case.save_settings(settings)
