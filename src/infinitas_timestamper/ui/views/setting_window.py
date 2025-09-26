from datetime import datetime, timedelta
from PySide6.QtWidgets import (
    QDialog,
    QLabel,
    QPushButton,
    QHBoxLayout,
    QLineEdit,
    QTextEdit,
    QFileDialog,
    QComboBox,
    QGridLayout,
    QWidget,
    QMessageBox,
)
from PySide6.QtGui import QIntValidator, QCloseEvent
from PySide6.QtCore import QThread
from pathlib import Path
from domain.entity.game_entity import ChartDetail, PlayData, PlayResult
from domain.entity.game_format import FormatID, GameTimestampFormatter
from domain.entity.settings_entity import (
    SettingTimestampFormat,
    Settings,
    SettingObs,
    SettingReflux,
    SettingYoutube,
)
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.value.game_value import DJ_LEVEL, ClearLamp
from infrastructure.obs_connector_v5 import OBSConnectorV5
from ui.views.utils import FunctionRunner


def create_sample_data() -> StreamSession[PlayData]:
    start_time = datetime.now()
    timestamps = [
        Timestamp[PlayData](
            occurred_at=start_time + timedelta(minutes=2, seconds=4),
            data=PlayData(
                title="BLUE ZONE",
                level=7,
                chart_detail=ChartDetail(
                    artist="Natsh & TAKAKI",
                    genre="SPEED RAVE",
                    bpm=123,
                    difficulty="SPN",
                    note_count=654,
                ),
                play_result=PlayResult(
                    dj_level=DJ_LEVEL.AAA,
                    lamp=ClearLamp.PERFECT,
                    p_great=512,
                    great=42,
                    good=0,
                    bad=0,
                    poor=3,
                    fast=20,
                    slow=22,
                    combo_break=0,
                ),
            ),
        ),
        Timestamp[PlayData](
            occurred_at=start_time + timedelta(minutes=4, seconds=31),
            data=PlayData(
                title="天空の日没",
                level=10,
                chart_detail=ChartDetail(
                    artist="Cube",
                    genre="ANTHEM",
                    bpm=188,
                    difficulty="SPH",
                    note_count=990,
                ),
                play_result=PlayResult(
                    dj_level=DJ_LEVEL.A,
                    lamp=ClearLamp.CLEAR,
                    p_great=624,
                    great=243,
                    good=77,
                    bad=25,
                    poor=21,
                    fast=173,
                    slow=193,
                    combo_break=18,
                ),
            ),
        ),
        Timestamp[PlayData](
            occurred_at=start_time + timedelta(minutes=7, seconds=12),
            data=PlayData(
                title="Sample Paradise",
                level=12,
                chart_detail=ChartDetail(
                    artist="Mamon",
                    genre="HARD TRANCE",
                    bpm=213,
                    difficulty="SPL",
                    note_count=2184,
                ),
                play_result=PlayResult(
                    dj_level=DJ_LEVEL.A,
                    lamp=ClearLamp.FAILED,
                    p_great=1987,
                    great=123,
                    good=43,
                    bad=14,
                    poor=18,
                    fast=107,
                    slow=90,
                    combo_break=20,
                ),
            ),
        ),
    ]
    return StreamSession[PlayData](start_time=start_time, timestamps=timestamps)


SAMPLE_SESSION_DATA = create_sample_data()


class SettingsDialog(QDialog):
    def __init__(self, parent: QWidget):
        super().__init__(parent)
        self.setWindowTitle("設定")
        self.setMinimumWidth(500)
        self._setting = Settings()

        self._thread: QThread | None = None
        self._runner: FunctionRunner | None = None

        # OBS
        self.obs_host = QLineEdit("")
        self.obs_port = QLineEdit("")
        self.obs_port.setValidator(QIntValidator(0, 65535))
        self.obs_password = QLineEdit("")
        self.obs_password.setEchoMode(QLineEdit.EchoMode.Password)
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

        # タイムスタンプ
        self.format_template = QLineEdit("")
        self.format_id_combo = QComboBox()
        for fmt in FormatID:
            display = f"{fmt.logical_name()} (${fmt.value})"
            self.format_id_combo.addItem(display, userData=fmt)
        add_button = QPushButton("追加")
        add_button.setFixedWidth(100)
        add_button.clicked.connect(self.insert_format_template)
        format_layout = QHBoxLayout()
        format_layout.addWidget(self.format_id_combo)
        format_layout.addWidget(add_button)

        format_preview_layout = QHBoxLayout()
        self.format_preview = QTextEdit("")
        self.format_preview.setReadOnly(True)
        self.format_template.textChanged.connect(self.update_preview)
        format_preview_layout.addWidget(QLabel("プレビュー"))
        format_preview_layout.addWidget(self.format_preview)

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
            (QLabel("タイムスタンプフォーマット"),),
            (self.format_template,),
            (format_layout,),
            (format_preview_layout,),
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

    def test_obs_connection(self) -> str:
        connector = OBSConnectorV5()
        try:
            obs_version, program_scene = connector.test_connect(
                host=self.obs_host.text(),
                port=int(self.obs_port.text()),
                password=self.obs_password.text(),
            )
            return (
                f"接続成功\n"
                f"・OBSバージョン：{obs_version}\n"
                f"・表示シーン：{program_scene}"
            )
        except Exception as e:
            return f"接続失敗: {e}"

    def _test_obs_connect(self) -> None:
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

    def _on_test_finished(self, success: bool, msg: str) -> None:
        if not self.isVisible():
            return

        self.obs_test_btn.setEnabled(True)
        self.obs_test_btn.setText("接続テスト")
        QMessageBox.information(self, "接続テスト結果", msg)
        self._thread = None
        self._runner = None

    def _browse_dir(self) -> None:
        dir_path = QFileDialog.getExistingDirectory(self, "Refluxフォルダを選択")
        if dir_path:
            self.reflux_dir.setText(dir_path)

    def insert_format_template(self) -> None:
        cursor_pos = self.format_template.cursorPosition()
        text = self.format_template.text()

        fmt: FormatID = self.format_id_combo.currentData()
        placeholder = f"${fmt}"

        new_text = text[:cursor_pos] + placeholder + text[cursor_pos:]
        self.format_template.setText(new_text)
        self.format_template.setCursorPosition(cursor_pos + len(placeholder))

    def update_preview(self) -> None:
        formatter = GameTimestampFormatter(self.format_template.text())

        rendered = "\n".join(
            formatter.format(SAMPLE_SESSION_DATA, timestamp)
            for timestamp in SAMPLE_SESSION_DATA.timestamps
        )
        self.format_preview.setPlainText(rendered)

    def open_dialog(self, setting: Settings) -> int:
        self._setting = setting
        self.obs_host.setText(setting.obs.host)
        self.obs_port.setText(str(setting.obs.port))
        self.obs_password.setText(setting.obs.password)
        self.reflux_dir.setText(str(setting.reflux.directory))
        self.format_template.setText(setting.timestamp.template)
        self.youtube_auth.setCurrentText(setting.youtube.auth_type)

        return super().exec()

    def get_setting(self) -> Settings:
        return self._setting

    def save_setting(self) -> None:
        self._setting = Settings(
            obs=SettingObs(
                host=self.obs_host.text(),
                port=int(self.obs_port.text()),
                password=self.obs_password.text(),
            ),
            reflux=SettingReflux(directory=Path(self.reflux_dir.text())),
            timestamp=SettingTimestampFormat(template=self.format_template.text()),
            youtube=SettingYoutube(auth_type=""),
        )
        self.accept()

    def closeEvent(self, arg__1: QCloseEvent) -> None:
        if hasattr(self, "_thread") and self._thread is not None:
            if self._thread.isRunning():
                self._thread.quit()
        return super().closeEvent(arg__1)
