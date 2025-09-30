import sys
from injector import Injector, Module, Binder, singleton, provider
from pathlib import Path

from PySide6.QtWidgets import QWidget

from core.arguments import Arguments
from domain.entity.game_entity import PlayData
from domain.value.base_path import BasePath
from domain.entity.settings_entity import Settings
from domain.port.play_watcher import IPlayWatcher
from domain.port.stream_gateway import IStreamGateway

from ui.factory.play_recording_widget_factory import PlayRecordingWidgetFactory
from ui.factory.update_window_factory import UpdateWindowFactory
from ui.factory.updater_thread_factory import UpdaterThreadFactory
from ui.thread.updater_thread import UpdaterThread
from ui.views.play_recording_widget import PlayRecordingWidget
from ui.views.update_window import UpdateWindow

from usecase.repository.settings_repository import SettingsRepository
from usecase.repository.stream_session_repository import StreamSessionRepository
from usecase.repository.current_stream_session_repository import (
    CurrentStreamSessionRepository,
)
from usecase.repository.app_updater import IAppUpdater
from usecase.repository.app_version_provider import IVersionProvider

from infrastructure.reflux_file_watcher import RefluxFileWatcher
from infrastructure.obs_connector_v5 import OBSConnectorV5
from infrastructure.file_setting_repository import FileSettingsRepository
from infrastructure.file_stream_session_repository import FileStreamSessionRepository
from infrastructure.in_memory_current_stream_session_repository import (
    InMemoryCurrentStreamSessionRepository,
)
from infrastructure.file_system_app_updater import FileSystemAppUpdater
from infrastructure.github_accessor import GithubRepositoryAccessor


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
        binder.bind(IAppUpdater, to=FileSystemAppUpdater, scope=singleton)  # type: ignore
        binder.bind(IVersionProvider, to=GithubRepositoryAccessor, scope=singleton)  # type: ignore

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
    def provide_updater_thread_factory(self, injector: Injector) -> UpdaterThreadFactory:
        def factory() -> UpdaterThread:
            return injector.create_object(UpdaterThread)

        return factory

    @singleton
    @provider
    def provide_update_window_factory(self, injector: Injector) -> UpdateWindowFactory:
        def factory(parent: QWidget | None = None) -> UpdateWindow:
            return injector.create_object(UpdateWindow, additional_kwargs={"parent": parent})

        return factory
