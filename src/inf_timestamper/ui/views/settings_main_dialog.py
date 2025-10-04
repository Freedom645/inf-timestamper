from PySide6.QtWidgets import (
    QDialog,
    QPushButton,
    QHBoxLayout,
    QVBoxLayout,
    QTabWidget,
    QWidget,
    QMessageBox,
)
from PySide6.QtGui import QCloseEvent
from domain.entity.settings_entity import Settings, SettingYoutube

from ui.views.settings_basic_tab import SettingsBasicTab
from ui.views.settings_stream_tab import SettingsStreamTab


class SettingsMainDialog(QDialog):
    def __init__(self, parent: QWidget):
        super().__init__(parent)
        self.setWindowTitle("設定")
        self.setMinimumWidth(500)
        self._setting = Settings()

        self.tabs = QTabWidget()
        self.basic_tab = SettingsBasicTab(self)
        self.stream_tab = SettingsStreamTab(self)

        self.tabs.addTab(self.basic_tab, "基本設定")
        self.tabs.addTab(self.stream_tab, "配信ソフト連携")

        # ボタン
        btn_layout = QHBoxLayout()
        save_btn = QPushButton("保存")
        save_btn.clicked.connect(self.save_setting)
        save_btn.setFixedWidth(100)
        cancel_btn = QPushButton("キャンセル")
        cancel_btn.clicked.connect(self.close)
        cancel_btn.setFixedWidth(100)
        btn_layout.addWidget(save_btn)
        btn_layout.addWidget(cancel_btn)

        # ダイアログ全体レイアウト
        main_layout = QVBoxLayout()
        main_layout.addWidget(self.tabs)
        main_layout.addLayout(btn_layout)
        self.setLayout(main_layout)

    def open_dialog(self, setting: Settings) -> int:
        self._setting = setting
        self.basic_tab.set_settings(setting.reflux, setting.timestamp)
        self.stream_tab.set_settings(setting.obs)

        return super().exec()

    def get_setting(self) -> Settings:
        return self._setting

    def save_setting(self) -> None:
        try:
            settings_reflux, settings_timestamp = self.basic_tab.get_settings()
            settings_obs = self.stream_tab.get_settings()
            self._setting = Settings(
                reflux=settings_reflux,
                obs=settings_obs,
                timestamp=settings_timestamp,
                youtube=SettingYoutube(auth_type=""),
            )
            self.accept()
        except Exception as e:
            QMessageBox.critical(self, "エラー", f"設定の保存に失敗しました: {e}")

    def closeEvent(self, arg__1: QCloseEvent) -> None:
        self.basic_tab.closeEvent(arg__1)
        self.stream_tab.closeEvent(arg__1)
        return super().closeEvent(arg__1)
