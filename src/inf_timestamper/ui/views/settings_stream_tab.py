import logging
from PySide6.QtWidgets import (
    QWidget,
    QGridLayout,
    QLabel,
    QCheckBox,
    QLineEdit,
    QPushButton,
    QMessageBox,
)
from PySide6.QtGui import QIntValidator, QCloseEvent
from PySide6.QtCore import QThread, Qt

from domain.entity.settings_entity import SettingObs
from infrastructure.obs_connector_v5 import OBSConnectorV5
from ui.views.utils import FunctionRunner


class SettingsStreamTab(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)

        self._thread: QThread | None = None
        self._runner: FunctionRunner | None = None

        layout = QGridLayout()

        self.obs_enabled = QCheckBox("OBS連携を有効にする")
        self.obs_enabled.checkStateChanged.connect(self._on_obs_enabled_changed)
        self.obs_host = QLineEdit("")
        self.obs_port = QLineEdit("")
        self.obs_port.setValidator(QIntValidator(0, 65535))
        self.obs_password = QLineEdit("")
        self.obs_password.setEchoMode(QLineEdit.EchoMode.Password)
        self.obs_test_btn = QPushButton("接続テスト")
        self.obs_test_btn.setFixedWidth(100)
        self.obs_test_btn.clicked.connect(self._on_test_obs_connect)

        grid_widgets: list[list[QWidget]] = [
            [self.obs_enabled],
            [QLabel("ホスト"), self.obs_host],
            [QLabel("ポート"), self.obs_port],
            [QLabel("パスワード"), self.obs_password],
            [self.obs_test_btn],
        ]
        for row, widgets in enumerate(grid_widgets):
            for col, widget in enumerate(widgets):
                row_span = 1
                column_span = 2 if len(widgets) == 1 else 1
                layout.addWidget(widget, row, col, row_span, column_span)

        self.setLayout(layout)

    def _on_obs_enabled_changed(self, state: Qt.CheckState) -> None:
        enabled = state == Qt.CheckState.Checked
        self._set_enabled_all_obs_settings(enabled)

    def _set_enabled_all_obs_settings(self, enabled: bool) -> None:
        self.obs_host.setEnabled(enabled)
        self.obs_port.setEnabled(enabled)
        self.obs_password.setEnabled(enabled)
        self.obs_test_btn.setEnabled(enabled)

    def _test_obs_connection(self) -> str:
        connector = OBSConnectorV5(logging.getLogger("app"))
        try:
            obs_version, program_scene = connector.test_connect(
                host=self.obs_host.text(),
                port=int(self.obs_port.text()),
                password=self.obs_password.text(),
            )
            return f"接続成功\n・OBSバージョン：{obs_version}\n・表示シーン：{program_scene}"
        except Exception as e:
            return f"接続失敗: {e}"

    def _on_test_obs_connect(self) -> None:
        self.obs_test_btn.setEnabled(False)
        self.obs_test_btn.setText("接続中...")

        self._thread = QThread()
        self._runner = FunctionRunner(self._test_obs_connection)
        self._runner.moveToThread(self._thread)

        self._thread.started.connect(self._runner.run)
        self._runner.finished.connect(self._on_test_finished)
        self._runner.finished.connect(self._thread.quit)
        self._runner.finished.connect(self._runner.deleteLater)
        self._thread.finished.connect(self._thread.deleteLater)

        self._thread.start()

    def _on_test_finished(self, success: bool, msg: str) -> None:
        if not self.isVisible():
            return

        self.obs_test_btn.setEnabled(True)
        self.obs_test_btn.setText("接続テスト")
        QMessageBox.information(self, "接続テスト結果", msg)
        self._thread = None
        self._runner = None

    def set_settings(self, obs_settings: SettingObs) -> None:
        self.obs_enabled.setChecked(obs_settings.is_enabled)
        self.obs_host.setText(obs_settings.host)
        self.obs_port.setText(str(obs_settings.port))
        self.obs_password.setText(obs_settings.password)
        self._set_enabled_all_obs_settings(obs_settings.is_enabled)

    def get_settings(self) -> SettingObs:
        return SettingObs(
            is_enabled=self.obs_enabled.isChecked(),
            host=self.obs_host.text(),
            port=int(self.obs_port.text()),
            password=self.obs_password.text(),
        )

    def closeEvent(self, arg__1: QCloseEvent) -> None:
        if self._thread is not None:
            if self._thread.isRunning():
                self._thread.quit()
        return super().closeEvent(arg__1)
