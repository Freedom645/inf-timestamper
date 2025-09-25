import sys
from injector import Module, Binder, singleton, provider
from pathlib import Path

from domain.value.base_path import BasePath
from domain.entity.settings import Settings
from domain.port.play_watcher import IPlayWatcher
from domain.port.stream_gateway import IStreamGateway

from usecase.repository.settings_repository import SettingsRepository
from usecase.repository.stream_session_repository import StreamSessionRepository

from infrastructure.reflux_file_watcher import RefluxFileWatcher
from infrastructure.obs_connector_v5 import OBSConnectorV5
from infrastructure.setting_repository import FileSettingsRepository
from infrastructure.stream_session_repository import FileStreamSessionRepository


class AppModule(Module):

    def configure(self, binder: Binder) -> None:
        binder.bind(SettingsRepository, to=FileSettingsRepository, scope=singleton)
        binder.bind(
            StreamSessionRepository, to=FileStreamSessionRepository, scope=singleton
        )
        binder.bind(IPlayWatcher, to=RefluxFileWatcher, scope=singleton)
        binder.bind(IStreamGateway, to=OBSConnectorV5, scope=singleton)

    @singleton
    @provider
    def provide_base_path(self) -> BasePath:
        if getattr(sys, "frozen", False):
            return BasePath(Path(sys.executable).parent)

        return BasePath(Path(__file__).resolve().parent / "app_resources_dev")

    @provider
    @singleton
    def provide_settings(self) -> Settings:
        return Settings()
