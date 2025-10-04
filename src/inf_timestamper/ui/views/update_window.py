from injector import inject
from typing import Callable
from PySide6.QtWidgets import QApplication, QWidget, QVBoxLayout, QProgressBar, QLabel, QMessageBox, QDialog
from PySide6.QtGui import QCloseEvent

from usecase.dto.app_updating import UpdateStep
from ui.thread.updater_thread import UpdaterThread
from ui.factory.updater_thread_factory import UpdaterThreadFactory


class UpdateWindow(QDialog):
    @inject
    def __init__(
        self,
        updater_thread_factory: UpdaterThreadFactory,
        parent: QWidget | None = None,
    ):
        super().__init__(parent)
        self._thread: UpdaterThread | None = None
        self._thread_factory = updater_thread_factory

        self.setWindowTitle("アップデート")
        self.setFixedSize(300, 300)

        layout = QVBoxLayout(self)

        # 各ステップバーとラベル
        self.steps: dict[UpdateStep, QProgressBar] = {}
        self.step_layouts: dict[UpdateStep, QVBoxLayout] = {}
        self.step_labels: dict[UpdateStep, str] = {
            UpdateStep.BACKUP: "バックアップ",
            UpdateStep.DOWNLOAD: "ダウンロード",
            UpdateStep.APPLY_UPDATE: "更新",
        }
        for step in UpdateStep:
            label = QLabel(self.step_labels[step])
            bar = QProgressBar()
            bar.setRange(0, 100)
            layout.addWidget(label)
            layout.addWidget(bar)
            self.steps[step] = bar

    def start_update(self) -> None:
        self._thread = self._thread_factory()
        self._thread.progress_changed.connect(self.update_step)
        self._thread.finished_update.connect(self.on_finished)
        self._thread.start()

    def update_step(self, name: UpdateStep, value: int) -> None:
        if name in self.steps:
            self.steps[name].setValue(value)

    def on_finished(self, success: bool, finalize: Callable[[], None]) -> None:
        if success:
            QMessageBox.information(self, "完了", "アップデートが完了しました。<br>アプリケーションを起動します。")
            finalize()
            QApplication.quit()
        else:
            QMessageBox.critical(self, "エラー", "アップデート中にエラーが発生しました。")

    def closeEvent(self, event: QCloseEvent) -> None:
        if self._thread and self._thread.isRunning():
            event.ignore()
        else:
            event.accept()
