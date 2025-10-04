from injector import inject
from typing import Callable
from packaging import version

from core.version import __version__
from usecase.dto.app_updating import StepProgressCallback, UpdateStep, VersionInfo
from usecase.repository.app_version_provider import IVersionProvider
from usecase.repository.app_updater import IAppUpdater


class AppUpdatingUseCase:
    @inject
    def __init__(self, app_updater: IAppUpdater, version_provider: IVersionProvider) -> None:
        self._app_updater = app_updater
        self._version_provider = version_provider

    def check_latest_version(self) -> VersionInfo:
        return self._version_provider.check_latest_version()

    def get_current_version(self) -> version.Version:
        return version.parse(__version__)

    def update_app(self, step_callback: StepProgressCallback, version_info: VersionInfo) -> Callable[[], None]:
        self._app_updater.backup(lambda p: step_callback(UpdateStep.BACKUP, p))
        self._app_updater.download(lambda p: step_callback(UpdateStep.DOWNLOAD, p), version_info)
        self._app_updater.apply_update(lambda p: step_callback(UpdateStep.APPLY_UPDATE, p))
        return self._app_updater.finalize
