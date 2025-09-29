from injector import inject

from domain.entity.update_entity import ExecutionResult
from domain.port.app_updater import IAppUpdater


class AppUpdatingUseCase:
    @inject
    def __init__(self, app_updater: IAppUpdater) -> None:
        self._app_updater = app_updater

    def check_latest_version(self) -> ExecutionResult:
        return self._app_updater.check()

    def update_app(self) -> None:
        self._app_updater.update()
        return
