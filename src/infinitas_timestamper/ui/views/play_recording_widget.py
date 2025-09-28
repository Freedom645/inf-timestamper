from pathlib import Path
from uuid import UUID
from PySide6.QtWidgets import (
    QWidget,
    QLabel,
    QGridLayout,
    QListWidget,
    QListWidgetItem,
    QPushButton,
    QSizePolicy,
    QFileDialog,
    QHBoxLayout,
)
from PySide6.QtGui import QCloseEvent
from PySide6.QtCore import QThread
from datetime import datetime
from injector import inject

from domain.entity.game_entity import PlayData
from domain.entity.game_format import GameTimestampFormatter
from domain.entity.settings_entity import Settings
from domain.entity.stream_entity import StreamSession, Timestamp
from domain.value.base_path import BasePath
from ui.view_models.play_recording_view_model import PlayRecordingViewModel
from ui.views.utils import FunctionRunner
from ui.widgets.date_time_edit import DateTimeEdit


class PlayRecordingWidget(QWidget):
    @inject
    def __init__(
        self,
        play_recording_view_model: PlayRecordingViewModel,
        base_path: BasePath,
        settings: Settings,
        parent: QWidget | None = None,
    ) -> None:
        super().__init__(parent)
        self._vm = play_recording_view_model
        self.base_path = base_path
        self.settings = settings

        self._thread: QThread | None = None
        self._timestamp_item_map: dict[UUID, QListWidgetItem] = {}

        self.start_btn = QPushButton("記録開始")
        self.start_btn.setMinimumSize(80, 40)
        self.start_btn.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.start_btn.clicked.connect(self.toggle_recording)

        self.copy_btn = QPushButton("コピー")
        self.copy_btn.setMinimumSize(80, 40)
        self.copy_btn.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.copy_btn.clicked.connect(self._vm.on_copy_timestamps_to_clipboard)
        self._vm.copy_button_changed.connect(self._on_copy_button_changed)

        btn_layout = QHBoxLayout()
        btn_layout.addWidget(self.start_btn)
        btn_layout.addWidget(self.copy_btn)

        self.status = QLabel("記録開始待ち")
        self.stream_start_time = DateTimeEdit()

        self.timestamp_count = QLabel("0")
        self.list_widget = QListWidget()

        grid_data = [
            [btn_layout],
            [QLabel("状態"), self.status],
            [QLabel("配信開始時間"), self.stream_start_time],
            [QLabel("タイムスタンプ数", self.timestamp_count)],
            [self.list_widget],
        ]

        layout = QGridLayout()
        for row_index, row in enumerate(grid_data):
            for column_index, item in enumerate(row):
                rowSpan = 1
                column_span = 2 if len(row) == 1 else 1
                if isinstance(item, QWidget):
                    layout.addWidget(item, row_index, column_index, rowSpan, column_span)
                else:
                    layout.addLayout(item, row_index, column_index, rowSpan, column_span)
        self.setLayout(layout)

        # Signal を接続
        self._vm.recording_button_changed.connect(self._on_recording_button_changed)
        self._vm.status_changed.connect(self._on_status_changed)
        self._vm.start_time_changed.connect(self._on_start_time_changed)
        self._vm.timestamp_count_changed.connect(self._on_timestamp_count_changed)
        self._vm.timestamp_upsert_signal.connect(self._on_timestamp_upsert_signal)
        self._vm.play_record_overwrite_signal.connect(self._on_overwrite_signal)

    def toggle_recording(self) -> None:
        if self.start_btn.text() == "記録開始":
            self._thread = QThread()
            self._worker = FunctionRunner(self._vm.on_start_recording_button)
            self._worker.moveToThread(self._thread)
            self._thread.started.connect(self._worker.run)
            self._worker.finished.connect(self._thread.quit)
            self._worker.finished.connect(self._worker.deleteLater)
            self._thread.finished.connect(self._thread.deleteLater)
            self._thread.start()
        else:
            self._vm.on_stop_recording_button()

    def open_recording(self) -> None:
        dir = self.base_path / "sessions"
        if not dir.exists():
            dir = dir.parent

        file_path, _ = QFileDialog.getOpenFileName(
            self,
            "ファイルを選択",
            dir=str(dir),
            filter="記録ファイル (*.json);;すべてのファイル (*)",
        )
        if file_path:
            self._vm.on_open_recording(Path(file_path))

    def _on_recording_button_changed(self, enabled: bool, text: str) -> None:
        self.start_btn.setEnabled(enabled)
        self.start_btn.setText(text)

    def _on_copy_button_changed(self, enabled: bool, text: str) -> None:
        self.copy_btn.setEnabled(enabled)
        self.copy_btn.setText(text)

    def _on_status_changed(self, status: str) -> None:
        self.status.setText(status)

    def _on_start_time_changed(self, start_time: datetime | None) -> None:
        if start_time:
            self.stream_start_time.set_datetime(start_time)
        else:
            self.stream_start_time.clear()

    def _on_timestamp_count_changed(self, count: int) -> None:
        self.timestamp_count.setText(str(count))

    def _on_timestamp_upsert_signal(self, session: StreamSession[PlayData], timestamp: Timestamp[PlayData]) -> None:
        formatter = GameTimestampFormatter(self.settings.timestamp.template)
        label = formatter.format(session, timestamp)

        if timestamp.id in self._timestamp_item_map:
            item = self._timestamp_item_map[timestamp.id]
            item.setText(label)
            return

        self._timestamp_item_map[timestamp.id] = QListWidgetItem(label)
        self.list_widget.addItem(self._timestamp_item_map[timestamp.id])
        self.list_widget.scrollToBottom()

    def _on_overwrite_signal(self, session: StreamSession[PlayData]) -> None:
        self._on_start_time_changed(session.start_time)
        self._on_timestamp_count_changed(session.count_timestamp())
        self._timestamp_item_map.clear()
        self.list_widget.clear()
        for timestamp in session.timestamps:
            self._vm.timestamp_upsert_signal.emit(session, timestamp)

    def closeEvent(self, event: QCloseEvent) -> None:
        self._vm.on_close()
        return super().closeEvent(event)
