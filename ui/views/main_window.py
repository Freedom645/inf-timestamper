import asyncio
from injector import inject
from PySide6.QtWidgets import (
    QMainWindow,
    QWidget,
    QVBoxLayout,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QListWidget,
    QSizePolicy,
    QMessageBox,
)
from PySide6.QtGui import QCloseEvent
from PySide6.QtCore import QThread

from ui.view_models.main_window_view_model import MainWindowViewModel
from ui.views.play_recording_widget import PlayRecordingWidget
from ui.views.setting_window import SettingsDialog
from ui.views.utils import FunctionRunner


class MainWindow(QMainWindow):

    @inject
    def __init__(
        self, vm: MainWindowViewModel, play_recording_widget: PlayRecordingWidget
    ):
        super().__init__()
        self.vm = vm
        self.play_recording_widget = play_recording_widget

        self.vm.get_settings()

        self.setWindowTitle("INFINITAS TimeStamper")
        self.setMinimumWidth(300)
        self.setMinimumHeight(500)
        self.menuBar().addAction("設定", self.open_settings)

        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        self.main_layout = QVBoxLayout(central_widget)

        self.main_layout.addWidget(self.play_recording_widget)

    def open_settings(self):
        dialog = SettingsDialog(self)
        if dialog.exec(setting=self.vm.get_settings()):
            self.vm.update_setting(dialog.get_setting())

    def closeEvent(self, event: QCloseEvent):
        event.accept()
