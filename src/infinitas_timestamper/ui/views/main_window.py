from injector import inject
from PySide6.QtWidgets import QMainWindow, QWidget, QVBoxLayout
from PySide6.QtGui import QCloseEvent

from core.version import __version__
from ui.factory.play_recording_widget_factory import PlayRecordingWidgetFactory
from ui.view_models.main_window_view_model import MainWindowViewModel
from ui.views.setting_window import SettingsDialog


class MainWindow(QMainWindow):

    @inject
    def __init__(
        self,
        vm: MainWindowViewModel,
        play_recording_widget_factory: PlayRecordingWidgetFactory,
    ):
        super().__init__()
        self.vm = vm
        self.play_recording_widget = play_recording_widget_factory(parent=self)

        self.vm.get_settings()

        self.setWindowTitle(f"INFINITAS TimeStamper {__version__}")
        self.setMinimumWidth(400)
        self.setMinimumHeight(500)
        file_menu = self.menuBar().addMenu("ファイル")
        file_menu.addAction("記録を開く", self.play_recording_widget.open_recording)
        file_menu.addAction("設定", self.open_settings)

        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        self.main_layout = QVBoxLayout(central_widget)

        self.main_layout.addWidget(self.play_recording_widget)

    def open_settings(self) -> None:
        dialog = SettingsDialog(self)
        if dialog.open_dialog(setting=self.vm.get_settings()):
            self.vm.update_setting(dialog.get_setting())

    def closeEvent(self, event: QCloseEvent) -> None:
        for child in self.findChildren(QWidget):
            if child is not self:
                child.close()
        event.accept()
