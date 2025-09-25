from PySide6.QtWidgets import (
    QDialog,
    QLabel,
    QPushButton,
    QHBoxLayout,
    QLineEdit,
    QFileDialog,
    QComboBox,
    QGridLayout,
    QWidget,
    QMessageBox,
)
from PySide6.QtGui import QIntValidator
from PySide6.QtCore import QThread
from pathlib import Path
from domain.entity.settings import Settings, SettingObs, SettingReflux, SettingYoutube
from infrastructure.obs_connector_v5 import OBSConnectorV5
from ui.views.utils import FunctionRunner


class SettingsDialog(QDialog):
    def __init__(self, parent):
        super().__init__(parent)
        self.setWindowTitle("設定")
        self.setMinimumWidth(400)
        self._setting = Settings()

        # OBS
        self.obs_host = QLineEdit("")
        self.obs_port = QLineEdit("")
        self.obs_port.setValidator(QIntValidator(0, 65535))
        self.obs_password = QLineEdit("")
        self.obs_test_btn = QPushButton("接続テスト")
        self.obs_test_btn.clicked.connect(self._test_obs_connect)
        self.obs_test_btn.setFixedWidth(100)

        # Reflux
        self.reflux_dir = QLineEdit("")
        self.reflux_dir.setReadOnly(True)
        browse_btn = QPushButton("参照")
        browse_btn.clicked.connect(self._browse_dir)
        dir_layout = QHBoxLayout()
        dir_layout.addWidget(self.reflux_dir)
        dir_layout.addWidget(browse_btn)

        # YouTube 認証方式
        self.youtube_auth = QComboBox()
        self.youtube_auth.addItems(["OAuth", "APIキー"])
        self.youtube_auth.setCurrentIndex(0)

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

        grid_data = [
            (QLabel("OBS"),),
            (QLabel("ホスト"), self.obs_host),
            (QLabel("ポート"), self.obs_port),
            (QLabel("パスワード"), self.obs_password),
            (self.obs_test_btn,),
            (QLabel("Reflux"),),
            (QLabel("フォルダ"), dir_layout),
            # (QLabel("YouTube"),),
            # (QLabel("認証方式"), self.youtube_auth),
            (btn_layout,),
        ]
        grid_layout = QGridLayout()
        grid_layout.setSpacing(10)
        for row_index, row in enumerate(grid_data):
            for column_index, e in enumerate(row):
                if isinstance(e, QWidget):
                    grid_layout.addWidget(
                        e, row_index, column_index, 1, 2 if len(row) == 1 else 1
                    )
                else:
                    grid_layout.addLayout(
                        e, row_index, column_index, 1, 2 if len(row) == 1 else 1
                    )
        self.setLayout(grid_layout)

    def test_obs_connection(self):
        connector = OBSConnectorV5()
        try:
            obs_version, scene_name = connector.test_connect(
                host=self.obs_host.text(),
                port=int(self.obs_port.text()),
                password=self.obs_password.text(),
            )
            return (
                f"接続成功: OBSバージョン {obs_version}, プレビューシーン {scene_name}"
            )
        except Exception as e:
            return f"接続失敗: {e}"

    def _test_obs_connect(self):
        self.obs_test_btn.setEnabled(False)
        self.obs_test_btn.setText("接続中...")

        self._thread = QThread()
        self._runner = FunctionRunner(self.test_obs_connection)
        self._runner.moveToThread(self._thread)

        self._thread.started.connect(self._runner.run)
        self._runner.finished.connect(self._on_test_finished)
        self._runner.finished.connect(self._thread.quit)
        self._runner.finished.connect(self._runner.deleteLater)
        self._thread.finished.connect(self._thread.deleteLater)

        self._thread.start()

    def _on_test_finished(self, success: bool, msg: str):
        if not self.isVisible():
            return

        self.obs_test_btn.setEnabled(True)
        self.obs_test_btn.setText("接続テスト")
        QMessageBox.information(self, "接続テスト結果", msg)
        self._thread = None
        self._runner = None

    def _browse_dir(self):
        dir_path = QFileDialog.getExistingDirectory(self, "Refluxフォルダを選択")
        if dir_path:
            self.reflux_dir.setText(dir_path)

    def exec(self, setting: Settings):
        self._setting = setting
        self.obs_host.setText(setting.obs.host)
        self.obs_port.setText(str(setting.obs.port))
        self.obs_password.setText(setting.obs.password)
        self.reflux_dir.setText(str(setting.reflux.directory))
        self.youtube_auth.setCurrentText(setting.youtube.auth_type)

        return super().exec()

    def get_setting(self) -> Settings:
        return self._setting

    def save_setting(self):
        self._setting = Settings(
            obs=SettingObs(
                host=self.obs_host.text(),
                port=int(self.obs_port.text()),
                password=self.obs_password.text(),
            ),
            reflux=SettingReflux(directory=Path(self.reflux_dir.text())),
            youtube=SettingYoutube(auth_type=""),
        )
        self.accept()

    def closeEvent(self, arg__1):
        if hasattr(self, "_thread") and self._thread is not None:
            if self._thread.isRunning():
                self._thread.quit()
        return super().closeEvent(arg__1)
