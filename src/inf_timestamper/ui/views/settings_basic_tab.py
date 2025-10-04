from datetime import datetime, timedelta
from pathlib import Path
from PySide6.QtWidgets import (
    QWidget,
    QGridLayout,
    QLabel,
    QLineEdit,
    QPushButton,
    QHBoxLayout,
    QFileDialog,
    QTextEdit,
    QComboBox,
)

from domain.entity.game_entity import ChartDetail, PlayData, PlayResult
from domain.entity.game_format import FormatID, GameTimestampFormatter
from domain.entity.settings_entity import SettingReflux, SettingTimestampFormat
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.value.game_value import DJ_LEVEL, ClearLamp
from ui.widgets.dollar_completer_line_edit import DollarCompleterLineEdit


class SettingsBasicTab(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        layout = QGridLayout()

        # Reflux
        self.reflux_dir = QLineEdit("")
        self.reflux_dir.setReadOnly(True)
        browse_btn = QPushButton("参照")
        browse_btn.clicked.connect(self._browse_dir)

        dir_layout = QHBoxLayout()
        dir_layout.addWidget(self.reflux_dir)
        dir_layout.addWidget(browse_btn)

        # タイムスタンプ
        self.format_template = DollarCompleterLineEdit([f"${f.value}" for f in FormatID])
        self.format_template.textChanged.connect(self._update_preview)
        self.format_id_combo = QComboBox()
        for fmt in FormatID:
            display = f"{fmt.logical_name()} (${fmt.value})"
            self.format_id_combo.addItem(display, userData=fmt)
        add_button = QPushButton("追加")
        add_button.setFixedWidth(100)
        add_button.clicked.connect(self._insert_format_template)
        format_layout = QHBoxLayout()
        format_layout.addWidget(self.format_id_combo)
        format_layout.addWidget(add_button)

        self.format_preview = QTextEdit("")
        self.format_preview.setReadOnly(True)

        # レイアウトへ追加
        layout.addWidget(QLabel("Refluxフォルダ"), 0, 0)
        layout.addLayout(dir_layout, 0, 1)
        layout.addWidget(QLabel("タイムスタンプフォーマット"), 1, 0, 1, 2)
        layout.addWidget(self.format_template, 2, 0, 1, 2)
        layout.addLayout(format_layout, 3, 0, 1, 2)
        layout.addWidget(QLabel("プレビュー"), 4, 0)
        layout.addWidget(self.format_preview, 4, 1)

        self.setLayout(layout)

    def _browse_dir(self) -> None:
        dir_path = QFileDialog.getExistingDirectory(self, "Refluxフォルダを選択")
        if dir_path:
            self.reflux_dir.setText(dir_path)

    def _insert_format_template(self) -> None:
        cursor_pos = self.format_template.cursorPosition()
        text = self.format_template.text()

        fmt: FormatID = self.format_id_combo.currentData()
        placeholder = f"${fmt}"

        new_text = text[:cursor_pos] + placeholder + text[cursor_pos:]
        self.format_template.setText(new_text)
        self.format_template.setCursorPosition(cursor_pos + len(placeholder))

    def _update_preview(self) -> None:
        formatter = GameTimestampFormatter(self.format_template.text())

        rendered = "\n".join(
            formatter.format(SAMPLE_SESSION_DATA, timestamp) for timestamp in SAMPLE_SESSION_DATA.timestamps
        )
        self.format_preview.setPlainText(rendered)

    def set_settings(self, reflux_settings: SettingReflux, timestamp_settings: SettingTimestampFormat) -> None:
        self.reflux_dir.setText(str(reflux_settings.directory))
        self.format_template.setText(timestamp_settings.template)
        self._update_preview()

    def get_settings(self) -> tuple[SettingReflux, SettingTimestampFormat]:
        reflux = SettingReflux(directory=Path(self.reflux_dir.text()))
        timestamp = SettingTimestampFormat(template=self.format_template.text())
        return reflux, timestamp


def create_sample_data() -> StreamSession[PlayData]:
    start_time = datetime.now()
    timestamps = [
        Timestamp[PlayData](
            occurred_at=start_time + timedelta(minutes=2, seconds=4),
            data=PlayData(
                key="test_1",
                chart_detail=ChartDetail(
                    title="BLUE ZONE",
                    level=7,
                    artist="Natsh & TAKAKI",
                    genre="SPEED RAVE",
                    bpm="123",
                    min_bpm="123",
                    max_bpm="123",
                    difficulty="SPN",
                    note_count=654,
                ),
                play_result=PlayResult(
                    dj_level=DJ_LEVEL.AAA,
                    gauge="EX HARD",
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
                key="test_2",
                chart_detail=ChartDetail(
                    title="天空の日没",
                    level=10,
                    artist="Cube",
                    genre="ANTHEM",
                    bpm="188",
                    min_bpm="188",
                    max_bpm="188",
                    difficulty="SPH",
                    note_count=990,
                ),
                play_result=PlayResult(
                    dj_level=DJ_LEVEL.A,
                    lamp=ClearLamp.CLEAR,
                    gauge="OFF",
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
                key="test_3",
                chart_detail=ChartDetail(
                    title="Sample Paradise",
                    level=12,
                    artist="Mamon",
                    genre="HARD TRANCE",
                    bpm="200~254",
                    min_bpm="200",
                    max_bpm="254",
                    difficulty="SPL",
                    note_count=2184,
                ),
                play_result=PlayResult(
                    dj_level=DJ_LEVEL.A,
                    lamp=ClearLamp.FAILED,
                    gauge="HARD",
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
