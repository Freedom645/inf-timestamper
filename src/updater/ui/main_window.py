# update_gui.py
import sys
from PySide6.QtWidgets import QApplication, QWidget, QVBoxLayout, QProgressBar, QLabel, QMessageBox
from PySide6.QtCore import QThread, Signal, QTimer

from domain.entity.arguments import Arguments
from domain.value.progress_value import Step
from domain.entity.result import ExecutionStatus
from usecase.update_use_case import UpdateUseCase


class UpdaterThread(QThread):
    progress_changed = Signal(Step, int)
    finished_update = Signal(bool)

    def __init__(self, args: Arguments, use_case: UpdateUseCase):
        super().__init__()
        self.args = args
        self.use_case = use_case

    def run(self):
        result = self.use_case.update(self.args, lambda step, progress: self.progress_changed.emit(step, progress))
        self.finished_update.emit(result.status == ExecutionStatus.SUCCESS)


class MainWindow(QWidget):
    def __init__(self, args: Arguments):
        super().__init__()
        self.args = args
        self.use_case = UpdateUseCase()
        self.setWindowTitle("アップデート")
        self.setFixedSize(300, 300)

        layout = QVBoxLayout(self)

        # 各ステップバーとラベル
        self.steps: dict[Step, QProgressBar] = {}
        self.step_layouts = {}
        for step in Step:
            label = QLabel(step.value)
            bar = QProgressBar()
            bar.setRange(0, 100)
            layout.addWidget(label)
            layout.addWidget(bar)
            self.steps[step] = bar

        QTimer.singleShot(100, self.start_update)

    def start_update(self):
        self._thread = UpdaterThread(self.args, self.use_case)
        self._thread.progress_changed.connect(self.update_step)
        self._thread.finished_update.connect(self.on_finished)
        self._thread.start()

    def update_step(self, name: Step, value: int):
        if name in self.steps:
            self.steps[name].setValue(value)

    def on_finished(self, success: bool):
        if success:
            for bar in self.steps.values():
                bar.setValue(100)
            reply = QMessageBox.information(
                self,
                "完了",
                "アップデートが完了しました。<br>アプリケーションを起動しますか？",
                QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            )
            if reply == QMessageBox.StandardButton.Yes:
                self.use_case.execute_app(self.args)
            QApplication.quit()
        else:
            QMessageBox.critical(self, "エラー", "アップデート中にエラーが発生しました。")


if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = MainWindow(Arguments.load_from_sysargs())
    window.show()
    sys.exit(app.exec())
