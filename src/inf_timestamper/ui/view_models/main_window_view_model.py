from injector import inject
from PySide6.QtCore import QObject
from enum import StrEnum


from core.arguments import ArgUpdateResult, Arguments
from domain.entity.settings_entity import Settings
from usecase.app_updating_use_case import AppUpdatingUseCase
from usecase.settings_use_case import SettingsUseCase


class DialogType(StrEnum):
    INFO = "info"
    QUESTION = "question"
    ERROR = "error"


class MainWindowViewModel(QObject):
    @inject
    def __init__(
        self, arguments: Arguments, settings_use_case: SettingsUseCase, app_updating_use_case: AppUpdatingUseCase
    ) -> None:
        QObject.__init__(self)
        self._arguments = arguments
        self._settings_use_case = settings_use_case
        self._app_updating_use_case = app_updating_use_case

    def get_settings(self) -> Settings:
        return self._settings_use_case.load_settings()

    def update_setting(self, settings: Settings) -> None:
        self._settings_use_case.save_settings(settings)

    def notify_update_result(self) -> str | None:
        match self._arguments.update_result:
            case ArgUpdateResult.SUCCESS:
                return "アプリが正常に更新されました。"
            case ArgUpdateResult.FAILED:
                return "アプリの更新中にエラーが発生しました。"
            case _:
                return None

    def check_app_latest(self) -> tuple[DialogType, str]:
        current_version = self._app_updating_use_case.get_current_version()
        latest_version_info = self._app_updating_use_case.check_latest_version()

        if latest_version_info.version > current_version:
            return (
                DialogType.QUESTION,
                f"最新バージョン v{latest_version_info.version} が利用可能です。更新しますか？<br>※アプリを再起動します。",
            )
        return DialogType.INFO, "最新バージョンです。"
