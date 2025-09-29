import sys
from injector import Injector, Module, Binder, singleton, provider
from pathlib import Path

from PySide6.QtWidgets import QWidget

from core.arguments import Arguments
from domain.entity.game_entity import PlayData
from domain.value.base_path import BasePath
from domain.entity.settings_entity import Settings
from domain.entity.update_entity import UpdateExeCommand
from domain.port.play_watcher import IPlayWatcher
from domain.port.stream_gateway import IStreamGateway
from domain.port.app_updater import IAppUpdater

from ui.factory.play_recording_widget_factory import PlayRecordingWidgetFactory
from ui.views.play_recording_widget import PlayRecordingWidget
from usecase.repository.settings_repository import SettingsRepository
from usecase.repository.stream_session_repository import StreamSessionRepository
from usecase.repository.current_stream_session_repository import (
    CurrentStreamSessionRepository,
)

from infrastructure.reflux_file_watcher import RefluxFileWatcher
from infrastructure.obs_connector_v5 import OBSConnectorV5
from infrastructure.file_setting_repository import FileSettingsRepository
from infrastructure.file_stream_session_repository import FileStreamSessionRepository
from infrastructure.in_memory_current_stream_session_repository import (
    InMemoryCurrentStreamSessionRepository,
)
from infrastructure.app_update_executer import AppUpdateExecuter


class AppModule(Module):
    def configure(self, binder: Binder) -> None:
        binder.bind(SettingsRepository, to=FileSettingsRepository, scope=singleton)  # type: ignore
        binder.bind(
            StreamSessionRepository[PlayData],  # type: ignore
            to=FileStreamSessionRepository,
            scope=singleton,
        )
        binder.bind(IPlayWatcher, to=RefluxFileWatcher, scope=singleton)  # type: ignore
        binder.bind(IStreamGateway, to=OBSConnectorV5, scope=singleton)  # type: ignore
        binder.bind(
            CurrentStreamSessionRepository[PlayData],  # type: ignore
            to=InMemoryCurrentStreamSessionRepository,
            scope=singleton,
        )
        binder.bind(IAppUpdater, to=AppUpdateExecuter, scope=singleton)  # type: ignore

    @singleton
    @provider
    def provide_base_path(self) -> BasePath:
        if getattr(sys, "frozen", False):
            return BasePath(Path(sys.executable).parent)

        return BasePath(Path(__file__).resolve().parent.parent / "app_resources_dev")

    @singleton
    @provider
    def provide_settings(self) -> Settings:
        return Settings()

    @singleton
    @provider
    def provide_arguments(self) -> Arguments:
        return Arguments.load()

    @singleton
    @provider
    def _provide_play_widget_factory(self, injector: Injector) -> PlayRecordingWidgetFactory:
        def factory(parent: QWidget | None = None) -> PlayRecordingWidget:
            # Injectorに解決させつつ、parentを渡してUIライフサイクルに載せる
            return injector.create_object(PlayRecordingWidget, additional_kwargs={"parent": parent})

        return factory

    @singleton
    @provider
    def provide_update_exe_command(self, base_path: BasePath) -> UpdateExeCommand:
        if getattr(sys, "frozen", False):
            return UpdateExeCommand(execution=[str(base_path / "updater.exe")])

        # FIXME: 実行コマンドどうにかしたい
        script_dir = Path("src") / "updater" / "main.py"
        return UpdateExeCommand(execution=["uv", "run", str(script_dir)])
