import logging
from injector import inject
from PySide6.QtCore import QThread, Signal

from usecase.dto.app_updating import UpdateStep
from usecase.app_updating_use_case import AppUpdatingUseCase


class UpdaterThread(QThread):
    progress_changed = Signal(UpdateStep, int)
    finished_update = Signal(bool, object)  # 成功/失敗, コールバック関数 () -> None

    @inject
    def __init__(self, logger: logging.Logger, use_case: AppUpdatingUseCase) -> None:
        super().__init__()
        self._use_case = use_case
        self._logger = logger

    def run(self) -> None:
        try:
            latest_version = self._use_case.check_latest_version()
            finalize = self._use_case.update_app(
                lambda step, progress: self.progress_changed.emit(step, progress), latest_version
            )
            self.finished_update.emit(True, finalize)
        except Exception as e:
            self._logger.error("アップデート中にエラーが発生しました。", exc_info=e)
            self.finished_update.emit(False, lambda: None)
