import sys
from injector import Injector, Module, Binder, singleton, provider
from pathlib import Path

from PySide6.QtWidgets import QWidget

from domain.entity.game_entity import PlayData
from domain.value.base_path import BasePath
from domain.entity.settings_entity import Settings
from domain.port.play_watcher import IPlayWatcher
from domain.port.stream_gateway import IStreamGateway

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
    def _provide_play_widget_factory(self, injector: Injector) -> PlayRecordingWidgetFactory:
        def factory(parent: QWidget | None = None) -> PlayRecordingWidget:
            # Injectorに解決させつつ、parentを渡してUIライフサイクルに載せる
            return injector.create_object(PlayRecordingWidget, additional_kwargs={"parent": parent})

        return factory
