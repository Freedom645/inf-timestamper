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
    QCheckBox,
)
from PySide6.QtCore import Qt

from domain.entity.sdvx_game_format import SDVXFormatID, SDVXGameTimestampFormatter
from domain.entity.sdvx_game_entity import SDVXChartDetail, SDVXPlayData, SDVXPlayResult
from domain.value.sdvx_game_value import SDVXClearLamp
from domain.entity.settings_entity import SettingSdvx
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.value.stream_value import StreamKind
from ui.widgets.dollar_completer_line_edit import DollarCompleterLineEdit


class SettingsSDVXTab(QWidget):
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

        self.format_start_label_enabled = QCheckBox("配信開始ラベルを含める")
        self.format_start_label_enabled.checkStateChanged.connect(self._on_format_start_label_enabled_changed)
        self.format_start_label = QLineEdit("00:00 配信開始")
        self.format_start_label.textChanged.connect(self._update_preview)

        # タイムスタンプ
        self.format_template = DollarCompleterLineEdit([f"${f.value}" for f in SDVXFormatID])
        self.format_template.textChanged.connect(self._update_preview)
        self.format_id_combo = QComboBox()
        for fmt in SDVXFormatID:
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
        layout_set: list[list[QWidget | QHBoxLayout]] = [
            [QLabel("sdvx_helperフォルダ"), dir_layout],
            [QLabel("タイムスタンプフォーマット")],
            [self.format_start_label_enabled, self.format_start_label],
            [self.format_template],
            [format_layout],
            [QLabel("プレビュー"), self.format_preview],
        ]
        for row, item in enumerate(layout_set):
            for col, widget in enumerate(item):
                row_span = 1
                column_span = 2 if len(item) == 1 else 1
                if isinstance(widget, QHBoxLayout):
                    layout.addLayout(widget, row, col, row_span, column_span)
                else:
                    layout.addWidget(widget, row, col, row_span, column_span)

        self.setLayout(layout)

    def _browse_dir(self) -> None:
        dir_path = QFileDialog.getExistingDirectory(self, "Refluxフォルダを選択")
        if dir_path:
            self.reflux_dir.setText(dir_path)

    def _on_format_start_label_enabled_changed(self, _: Qt.CheckState) -> None:
        self.format_start_label.setEnabled(self.format_start_label_enabled.isChecked())
        self._update_preview()

    def _insert_format_template(self) -> None:
        cursor_pos = self.format_template.cursorPosition()
        text = self.format_template.text()

        fmt: SDVXFormatID = self.format_id_combo.currentData()
        placeholder = f"${fmt}"

        new_text = text[:cursor_pos] + placeholder + text[cursor_pos:]
        self.format_template.setText(new_text)
        self.format_template.setCursorPosition(cursor_pos + len(placeholder))

    def _update_preview(self) -> None:
        formatter = SDVXGameTimestampFormatter(self.format_template.text())

        lines: list[str] = []
        if self.format_start_label_enabled.isChecked():
            lines.append(self.format_start_label.text())

        lines.extend(formatter.format(SAMPLE_SESSION_DATA, timestamp) for timestamp in SAMPLE_SESSION_DATA.timestamps)

        rendered = "\n".join(lines)
        self.format_preview.setPlainText(rendered)

    def set_settings(self, sdvx_settings: SettingSdvx) -> None:
        self.reflux_dir.setText(str(sdvx_settings.sdvx_helper_directory))
        self.format_start_label_enabled.setChecked(sdvx_settings.include_start_label)
        self.format_start_label.setText(sdvx_settings.start_label)
        self.format_start_label.setEnabled(sdvx_settings.include_start_label)
        self.format_template.setText(sdvx_settings.template)
        self._update_preview()

    def get_settings(self) -> SettingSdvx:
        return SettingSdvx(
            sdvx_helper_directory=Path(self.reflux_dir.text()),
            include_start_label=self.format_start_label_enabled.isChecked(),
            start_label=self.format_start_label.text(),
            template=self.format_template.text(),
        )


def create_sample_data() -> StreamSession:
    start_time = datetime.now()
    timestamps = [
        Timestamp(
            occurred_at=start_time + timedelta(minutes=2, seconds=4),
            data=SDVXPlayData(
                key="test_1",
                chart_detail=SDVXChartDetail(title="P4R4DISE ～虹の向こう～", level=7, difficulty="NOV"),
                play_result=SDVXPlayResult(score=95_0000, ex_score=1300, clear_lamp=SDVXClearLamp.CLEAR),
            ),
        ),
        Timestamp(
            occurred_at=start_time + timedelta(minutes=4, seconds=31),
            data=SDVXPlayData(
                key="test_2",
                chart_detail=SDVXChartDetail(title="Garakuta Soul Pray", level=13, difficulty="ADV"),
                play_result=SDVXPlayResult(score=99_1567, ex_score=3210, clear_lamp=SDVXClearLamp.UC),
            ),
        ),
        Timestamp(
            occurred_at=start_time + timedelta(minutes=7, seconds=12),
            data=SDVXPlayData(
                key="test_3",
                chart_detail=SDVXChartDetail(title="999", level=20, difficulty="MXM"),
                play_result=SDVXPlayResult(score=100_0000, ex_score=6404, clear_lamp=SDVXClearLamp.PUC),
            ),
        ),
    ]
    return StreamSession(kind=StreamKind.INF, start_time=start_time, timestamps=timestamps)


SAMPLE_SESSION_DATA = create_sample_data()
