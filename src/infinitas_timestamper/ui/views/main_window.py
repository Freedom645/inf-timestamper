from injector import inject
from PySide6.QtWidgets import QMainWindow, QWidget, QVBoxLayout, QMessageBox, QApplication
from PySide6.QtGui import QCloseEvent

from core.version import __version__
from ui.factory.play_recording_widget_factory import PlayRecordingWidgetFactory
from ui.view_models.main_window_view_model import DialogType, MainWindowViewModel
from ui.views.settings_main_dialog import SettingsMainDialog


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

        version_menu = self.menuBar().addMenu("バージョン情報")
        version_menu.addAction("更新の確認", self.check_for_updates)

        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        self.main_layout = QVBoxLayout(central_widget)

        self.main_layout.addWidget(self.play_recording_widget)

    def show(self) -> None:
        message = self.vm.notify_update_result()
        super().show()

        # 更新結果の通知
        if message:
            QMessageBox.information(self, "アップデート結果", message)
            return

        # アップデートチェック
        self.check_for_updates(for_auto_check=True)

        return

    def open_settings(self) -> None:
        dialog = SettingsMainDialog(self)
        if dialog.open_dialog(setting=self.vm.get_settings()):
            self.vm.update_setting(dialog.get_setting())

    def check_for_updates(self, *, for_auto_check: bool = False) -> None:
        dialog_type, message = self.vm.check_app_latest()
        if for_auto_check and dialog_type != DialogType.QUESTION:
            # 自動チェックの場合、更新がある場合のみ通知
            return

        if dialog_type == DialogType.INFO:
            QMessageBox.information(self, "バージョン情報", message)
        elif dialog_type == DialogType.QUESTION:
            reply = QMessageBox.question(self, "バージョン情報", message)
            if reply == QMessageBox.StandardButton.Yes:
                self.vm.update_app()
                QApplication.quit()
        else:  # dialog_type == DialogType.ERROR
            QMessageBox.critical(self, "バージョン情報", message)

    def closeEvent(self, event: QCloseEvent) -> None:
        for child in self.findChildren(QWidget):
            if child is not self:
                child.close()
        event.accept()
